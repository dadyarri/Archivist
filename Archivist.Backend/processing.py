import json
import os
import re
import subprocess
import threading
import time
from typing import Any

import ollama

from config import ProcessingMessage, Context
from progress import ProgressSender

STAGES = {
    "Транскрибация": lambda ctx, sender: _transcribe_stage(ctx, sender),
    "Сортировка": lambda ctx, sender: _sort_stage(ctx, sender),
    "Суммаризация": lambda ctx, sender: _summarize_stage(ctx, sender),
    "Генерация страницы": lambda ctx, sender: _generate_page_stage(ctx, sender),
}


def process_session(
    message: ProcessingMessage, context, shutdown_event: threading.Event
) -> None:
    proc_ctx = Context(
        input_files=message.get("files"),
        vault_path=message.get("vault"),
        output_subdirectory=message.get("subdirectory"),
        filename_format=message.get("format"),
        total_files=len(message.get("files")),
    )

    with ProgressSender(context) as sender:
        try:
            sender.send_progress("Инициализация", 0, "Запуск обработки сессии")
            for stage_name, stage_func in STAGES.items():
                success = stage_func(proc_ctx, sender)
                if not success:
                    raise RuntimeError(f"Stage {stage_name} failed")
                sender.send_progress(stage_name, 100, f"{stage_name} завершена")

            sender.send_finish(proc_ctx.output_path)
            shutdown_event.set()
        except Exception as error:
            sender.send_error(str(error))
            raise


def _transcribe_stage(ctx: Context, sender) -> bool:
    WHISPER_EXE = r"D:\oss\whisper.cpp\build\bin\whisper-cli.exe"
    MODEL_PATH = r"D:\oss\whisper.cpp\ggml-large-v1.bin"

    # Regex for lines like:
    # [00:00:00.000 --> 00:00:13.000]   The terrible bloody war began 100 years ago ...
    segment_line_re = re.compile(
        r"\[(\d{2}):(\d{2}):(\d{2}\.\d{3}) --> (\d{2}):(\d{2}):(\d{2}\.\d{3})\]\s+(.*)"
    )

    ctx.segments.clear()

    for i, file_info in enumerate(ctx.input_files):
        character = file_info["character"]
        audio_path = file_info["path"]
        stage_progress = int((i / ctx.total_files) * 100)
        sender.send_progress("Транскрибация", stage_progress, f"Запуск обработки {character}")

        # Prepare subprocess
        cmd = [
            WHISPER_EXE,
            "-f", audio_path,
            "-m", MODEL_PATH,
        ]

        # Store segments for this character
        character_segments = []

        try:
            proc = subprocess.Popen(
                cmd,
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,  # combine stderr to stdout
                universal_newlines=True,
                bufsize=1,
            )
        except Exception as e:
            ctx.errors.append(f"Failed to start whisper-cli for {audio_path}: {e}")
            continue

        line_num = 0
        segment_lines_count = 0
        segment_lines = []

        # We will buffer segment lines to estimate total segments for better progress updates

        # First pass: collect all segment lines
        # Reading line by line to avoid blocking
        for line in proc.stdout:
            line_num += 1
            m = segment_line_re.match(line)
            if m:
                segment_lines.append(line)
                segment_lines_count += 1

        proc.wait()
        if proc.returncode != 0:
            ctx.errors.append(f"whisper-cli failed for {audio_path} with code {proc.returncode}")
            continue

        sender.send_progress(
            "Транскрибация",
            int(((i + 0.98) / ctx.total_files) * 100),
            f"Обработка сегментов {character}"
        )

        # Process the segments, send progress within the file
        for j, line in enumerate(segment_lines):
            m = segment_line_re.match(line)
            if m:
                # Parse times
                h1, m1, s1 = int(m.group(1)), int(m.group(2)), float(m.group(3))
                h2, m2, s2 = int(m.group(4)), int(m.group(5)), float(m.group(6))
                phrase = m.group(7).strip()

                start_time = h1 * 3600 + m1 * 60 + s1
                end_time = h2 * 3600 + m2 * 60 + s2

                character_segments.append({
                    "start": start_time,
                    "end": end_time,
                    "phrase": phrase
                })

            # Update progress relative to this file's segments
            inner_progress = int((j + 1) / segment_lines_count * 100) if segment_lines_count > 0 else 100
            # Calculate overall stage progress:
            total_progress = int(((i + inner_progress / 100) / ctx.total_files) * 100)
            sender.send_progress("Транскрибация", total_progress, f"Обработка {character}")

        sender.send_progress(
            "Транскрибация",
            int(((i + 0.99) / ctx.total_files) * 100),
            f"Удаление дублей {character}"
        )

        ctx.segments.append({character: __merge_duplicated_segments(character_segments)})

    # After processing all files, final progress to 100%
    sender.send_progress("Транскрибация", 100, "Завершено")

    return True


def _sort_stage(ctx: Context, sender) -> bool:
    sender.send_progress("Сортировка", 0, "Собираем сессию в хронологическом порядке")

    combined_segments = []
    # ctx.segments is a list of dicts {character: [segments]}
    for seg_dict in ctx.segments:
        for character, segments in seg_dict.items():
            for seg in segments:
                combined_segments.append({
                    "character": character,
                    "startTime": seg["start"],
                    "endTime": seg["end"],
                    "phrase": seg["phrase"]
                })

    # Sort the combined list by start time
    combined_segments.sort(key=lambda x: x["startTime"])

    # Store in ctx.sorted_sessions as per your definition.
    ctx.sorted_segments = combined_segments

    sender.send_progress("Сортировка", 100, "Сессия отсортирована")

    return True


def _summarize_stage(ctx: Context, sender) -> bool:
    sender.send_progress("Суммаризация", 0, "Генерация саммари")

    # Prepare prompt with JSON data inserted
    prompt = f"""
Ты — помощник, задача которого — преобразовать диалоговую расшифровку игры в Dungeons & Dragons в плавный художественный рассказ.

Входящие данные — отсортированный JSON-массив, где каждый элемент имеет поля:

[
  {{
    "character": "имя-персонажа",
    "startTime": "время-начала-реплики",
    "endTime": "время-конца-реплики",
    "phrase": "реплика-на-английском"
  }}
]

Описание задачи:

1. Перепиши все реплики в форме художественного текста на русском языке.
2. Сохрани и пронумеруй всех персонажей по именам, которые встречаются в поле "character". Имена могут быть транслитерированы или искажены, но важно сохранять их в тексте последовательно, без изменений.
3. Повествование должно быть непрерывным и хронологическим — следуй строго порядку элементов массива.
4. Не включай в текст никаких мета-сведений, игровых механик, правил или терминов, относящихся к игре или её процессу.
5. Переводи смысл реплик, придавая им литературную форму: отделяй описание действий и реакций персонажей, добавляй связки и контекст, чтобы текст читалcя как художественный рассказ.
6. Если в репликах есть эмоции, интонации или действия, попытайся их передать через художественные средства (описания, эпитеты, мимику и т.п.).
7. Следи за плавностью и логикой переходов, используя подходящие связующие слова, но не добавляй ничего, чего нет в исходном тексте.
8. Не изменяй сюжет и смысл сказанного.

---

Входные данные (JSON):

{json.dumps(ctx.sorted_segments, ensure_ascii=False, indent=2)}
"""

    client = ollama.Client()

    response = client.chat(
        model="gpt-oss:20b",
        messages=[{'role': 'user', 'content': prompt}],
    )

    summary_text = response['message']['content'].strip()

    # Save summary to context and file
    ctx.summary = summary_text
    with open(r"D:\DnDRecordings\2025-09-13\Summary.txt", "w", encoding="utf-8") as f:
        f.write(summary_text)

    sender.send_progress("Суммаризация", 100, "Саммари успешно сгенерировано")
    return True


def _generate_page_stage(ctx: Context, sender: ProgressSender) -> bool:
    title = "Название"  # пока фиксируем Title

    # 1. Формируем полный путь к директории для выхода
    output_dir = os.path.join(ctx.vault_path, ctx.output_subdirectory)
    os.makedirs(output_dir, exist_ok=True)

    # 2. Строим regex для поиска существующих файлов
    # Экранируем спецсимволы в формате кроме {number} и {name}
    regex_pattern = re.escape(ctx.filename_format)
    regex_pattern = regex_pattern.replace(r"\{number\}", r"(\d+)")
    regex_pattern = regex_pattern.replace(r"\{name\}", r".*?")
    regex_pattern = "^" + regex_pattern + "$"

    sender.send_progress("Генерация страницы", 0, "Создание документа в Obsidian")

    # 3. Находим все файлы в output_dir, которые подходят под формат
    existing_files = os.listdir(output_dir)
    numbers = []
    for f in existing_files:
        match = re.match(regex_pattern, f)
        if match:
            numbers.append(int(match.group(1)))
    next_number = max(numbers, default=0) + 1

    # 4. Создаём новый файл с правильным именем
    filename = ctx.filename_format.replace("{name}", title).replace(
        "{number}", str(next_number)
    )
    filename = filename + ".md"
    output_path = os.path.join(output_dir, filename)
    with open(output_path, "w", encoding="utf-8") as f:
        f.write(f"# {title}\n\n")  # можно добавить шаблон содержимого

    ctx.output_path = output_path

    sender.send_progress("Генерация страницы", 50, "Расстановка ссылок на лор")
    time.sleep(5)

    return True


def __merge_duplicated_segments(segments: list[dict[str, Any]]) -> list[dict[str, Any]]:
    if not segments:
        return []

    merged = []
    prev = segments[0]

    for current in segments[1:]:
        if current["phrase"] == prev["phrase"] and abs(current["start"] - prev["end"]) < 0.001:
            # Extend the previous segment's end time
            prev["end"] = current["end"]
        else:
            # Different phrase or discontinuous time, push previous and advance
            merged.append(prev)
            prev = current

    merged.append(prev)
    return merged
