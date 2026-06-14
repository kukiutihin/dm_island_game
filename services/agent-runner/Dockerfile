FROM python:3.13-alpine
WORKDIR /agents
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY . .
CMD ["python", "-u", "agent.py"]
