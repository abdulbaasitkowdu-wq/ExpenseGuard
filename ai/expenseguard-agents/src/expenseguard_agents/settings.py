from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_prefix="EXPENSEGUARD_", extra="ignore")
    fake_mode: bool = True
    gemini_api_key: str | None = None
    gemini_model: str = "gemini-3.8-flash"
    postgres_dsn: str | None = None
    aspnet_base_url: str = "http://localhost:5000"
    aspnet_service_token: str | None = None
    tool_timeout_seconds: float = 5.0
    tool_retries: int = 2

    def validate_runtime(self) -> None:
        if not self.fake_mode and not self.gemini_api_key:
            raise RuntimeError("EXPENSEGUARD_GEMINI_API_KEY is required outside fake mode")
        if not self.fake_mode and not self.postgres_dsn:
            raise RuntimeError("EXPENSEGUARD_POSTGRES_DSN is required outside fake mode")
