from typing import Protocol
from .settings import Settings


class TextProvider(Protocol):
    async def generate(self, prompt: str) -> str: ...


class DeterministicFakeProvider:
    async def generate(self, prompt: str) -> str:
        return "FAKE_PROVIDER_RESULT"


def create_provider(settings: Settings) -> TextProvider:
    if settings.fake_mode:
        return DeterministicFakeProvider()
    from langchain_google_genai import ChatGoogleGenerativeAI
    return ChatGoogleGenerativeAI(
        model=settings.gemini_model,
        google_api_key=settings.gemini_api_key,
        temperature=0,
    )
