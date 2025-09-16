import logging
import threading
import zmq

from processing import process_session

logger = logging.getLogger(__name__)


class ZMQServer:
    def __init__(self) -> None:
        self.context = zmq.Context()
        self.command_socket = self.context.socket(zmq.REP)
        self.command_socket.bind("tcp://127.0.0.1:5555")
        logger.info("Command socket (REP) started on port 5555")

    def __enter__(self) -> "ZMQServer":
        return self

    def __exit__(self, exc_type, exc_val, exc_tb) -> None:
        self.command_socket.close()
        self.context.term()
        logger.info("ZeroMQ server shutdown")

    def listen(
        self, shutdown_event: threading.Event
    ) -> None:  # Add shutdown_event param
        while not shutdown_event.is_set():  # Check event in loop
            try:
                message = self.command_socket.recv_json()
                command = message.get("command")
                logger.info(f"Received command: {command}")

                if command == "start":
                    self.command_socket.send_json({"type": "accepted"})
                    proc_thread = threading.Thread(
                        target=process_session,
                        args=(message, self.context, shutdown_event),  # Pass event
                    )
                    proc_thread.start()
                    proc_thread.join()  # Wait for processing to finish
                    shutdown_event.set()  # Signal shutdown after join
                    break  # Exit loop after single session
                elif command == "ping":
                    self.command_socket.send_json({"type": "pong"})
                elif command == "stop":
                    self.command_socket.send_json({"type": "accepted"})
                    shutdown_event.set()
                    break
            except zmq.ZMQError as e:
                logger.error(f"ZMQ error: {e}", exc_info=True)
                break
            except Exception as e:
                logger.error(f"Exception: {e}", exc_info=True)
                break
