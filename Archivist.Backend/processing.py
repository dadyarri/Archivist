import os
import re
import threading
import time
import whisper

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
                success = stage_func(message, sender)
                if not success:
                    raise RuntimeError(f"Stage {stage_name} failed")
                sender.send_progress(stage_name, 100, f"{stage_name} завершена")

            sender.send_finish(proc_ctx.output_path)
            shutdown_event.set()
        except Exception as error:
            sender.send_error(str(error))
            raise


def _transcribe_stage(ctx: Context, sender: ProgressSender) -> bool:
    model = whisper.load_model("medium")

    for i, file_info in enumerate(ctx.input_files):
        character = file_info["character"]
        stage_progress = int(((i + 1) / ctx.total_files) * 100)
        sender.send_progress("Транскрибация", stage_progress, f"Обработка {character}")
        result = model.transcribe(audio=file_info["path"])

    return True


def _sort_stage(ctx: Context, sender: ProgressSender) -> bool:
    sender.send_progress("Сортировка", 0, "Собираем сессию в хронологическом порядке")
    time.sleep(2)  # Simulated
    return True


def _summarize_stage(ctx: Context, sender: ProgressSender) -> bool:
    sender.send_progress("Суммаризация", 0, "Генерация саммари")
    time.sleep(5)  # Simulated LLM call
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
