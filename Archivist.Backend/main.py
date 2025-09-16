#!/usr/bin/env python3
import sys
import threading
import logging
from zmq_handler import ZMQServer
from config import setup_logging

if __name__ == "__main__":
    setup_logging()
    logger = logging.getLogger(__name__)
    logger.info("Starting Archivist server")

    shutdown_event = threading.Event()  # Create shared event

    with ZMQServer() as server:
        server.listen(shutdown_event)

    logger.info("Server finished; exiting process")
    sys.exit(0)  # Graceful exit
