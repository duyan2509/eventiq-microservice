# Eventiq.AnalyticsService — Text2SQL pipeline (FastAPI + Groq)
FROM python:3.11-slim AS base

ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PORT=8080

WORKDIR /app

# Dependencies first for layer caching
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# App source
COPY src ./src

EXPOSE 8080
# src.api:app is the FastAPI instance (see src/api.py)
CMD ["uvicorn", "src.api:app", "--host", "0.0.0.0", "--port", "8080"]
