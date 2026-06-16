import os

# Disable the inter-step sleep before agent_loop is imported (it reads this at import time).
os.environ.setdefault("STEP_DELAY_MS", "0")
