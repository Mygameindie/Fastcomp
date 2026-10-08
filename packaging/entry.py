"""PyInstaller entry point for the standalone fastcomp executable."""
import sys

from fastcomp.cli import main

if __name__ == "__main__":
    if len(sys.argv) == 1:
        # Double-clicked instead of run from a terminal: show usage and keep the window open.
        try:
            main(["--help"])
        except SystemExit:
            pass
        input("\nRun this from a terminal, e.g. `fastcomp plan`. Press Enter to close.")
        sys.exit(0)
    sys.exit(main())
