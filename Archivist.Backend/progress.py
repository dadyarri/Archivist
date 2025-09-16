import zmq
from typing import AnyStr

from config import STAGE_WEIGHTS, STAGE_START_PERCENT


def _get_overall_percentage(stage_name: str, stage_progress: int) -> int:
    if stage_name not in STAGE_WEIGHTS:
        raise ValueError(f"Unknown stage: {stage_name}")
    start_offset = STAGE_START_PERCENT[stage_name]
    weight = STAGE_WEIGHTS[stage_name]
    normalized = stage_progress / 100.0
    return int((start_offset + (normalized * weight)) * 100)


class ProgressSender:
    def __init__(self, context: zmq.Context) -> None:
        self.socket = context.socket(zmq.PUSH)
        self.socket.connect("tcp://127.0.0.1:5556")

    def __enter__(self) -> "ProgressSender":
        return self

    def __exit__(self, exc_type, exc_val, exc_tb) -> None:
        self.socket.close()

    def send_progress(
        self, stage: AnyStr, percentage: int, message: AnyStr = ""
    ) -> None:
        overall = _get_overall_percentage(stage, percentage)
        msg = {
            "type": "progress",
            "stage": stage,
            "percentage": overall,
            "message": message,
        }
        self.socket.send_json(msg)
        # Log via injected logger if needed

    def send_finish(self, output_path: str) -> None:
        self.socket.send_json({"type": "finish", "message": output_path})

    def send_error(self, error_msg: str) -> None:
        self.socket.send_json({"type": "error", "message": error_msg})
