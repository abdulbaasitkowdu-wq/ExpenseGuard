import os
from collections.abc import Callable
from typing import TypeVar

from pydantic import BaseModel


ModelCall = Callable[[str, str], str]
TModel = TypeVar("TModel", bound=BaseModel)


def parse_model_json(model_cls: type[TModel], raw: str) -> TModel:
    text = raw.strip().removeprefix("```json").removeprefix("```").removesuffix("```").strip()
    return model_cls.model_validate_json(text)


def gemini_from_environment() -> ModelCall:
    api_key = os.environ.get("GEMINI_API_KEY")
    if not api_key:
        raise RuntimeError("GEMINI_API_KEY is required when the Gemini gateway is used.")
    model_name = os.environ.get("GEMINI_MODEL", "gemini-3.8-flash")

    def call(system_instruction: str, payload: str) -> str:
        from google import genai

        client = genai.Client(api_key=api_key)
        response = client.models.generate_content(
            model=model_name,
            contents=payload,
            config={"system_instruction": system_instruction, "response_mime_type": "application/json"},
        )
        if not response.text:
            raise ValueError("Gemini returned an empty response.")
        return response.text

    return call
