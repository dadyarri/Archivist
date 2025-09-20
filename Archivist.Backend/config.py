import logging
from dataclasses import dataclass, field
from typing import TypedDict, List, Dict, Any


class InputFile(TypedDict):
    path: str
    character: str


class ProcessingMessage(TypedDict):
    command: str
    files: list[InputFile]
    vault: str
    subdirectory: str
    format: str


@dataclass
class Context:
    input_files: List[InputFile] = field(default_factory=list)
    vault_path: str = ""
    output_subdirectory: str = ""
    filename_format: str = ""

    segments: List[Dict[str, Any]] = field(default_factory=list)
    sorted_segments: List[Dict[str, Any]] = field(default_factory=list)
    summary: str = ""
    final_content: str = ""
    output_path: str = ""

    total_files: int = 0
    errors: List[str] = field(default_factory=list)


LOG_PATH = r"D:\Archivist\Archivist.log"


def setup_logging() -> None:
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
        filename=LOG_PATH,
    )


# Stage weights (ensure they sum to ~1.0)
STAGE_WEIGHTS: dict[str, float] = {
    "Инициализация": 0.01,
    "Транскрибация": 0.40,
    "Сортировка": 0.04,
    "Суммаризация": 0.50,
    "Генерация страницы": 0.05,
}

# Precompute start percentages for efficiency
STAGE_START_PERCENT: dict[str, float] = {}
current_percent = 0.0
for stage, weight in STAGE_WEIGHTS.items():
    STAGE_START_PERCENT[stage] = current_percent
    current_percent += weight

assert abs(sum(STAGE_WEIGHTS.values()) - 1.0) < 0.001, "STAGE_WEIGHTS must sum to 1.0"
