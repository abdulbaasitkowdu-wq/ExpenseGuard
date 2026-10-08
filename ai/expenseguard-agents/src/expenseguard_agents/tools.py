import asyncio
from typing import Any
import httpx
from .contracts import ToolResult


ALLOWED_TOOLS: dict[str, tuple[str, str]] = {
    "get_reimbursement": ("GET", "/api/reimbursements/{reimbursement_id}"),
    "approve_reimbursement": ("POST", "/api/reimbursements/{reimbursement_id}/approve"),
    "reject_reimbursement": ("POST", "/api/reimbursements/{reimbursement_id}/reject"),
    "revise_reimbursement": ("POST", "/api/reimbursements/{reimbursement_id}/revise"),
}


class AspNetToolClient:
    def __init__(self, base_url: str, token: str | None, timeout: float = 5, retries: int = 2):
        self.base_url, self.token, self.timeout, self.retries = base_url, token, timeout, retries

    async def call(self, tool: str, arguments: dict[str, Any]) -> ToolResult:
        if tool not in ALLOWED_TOOLS:
            return ToolResult(ok=False, error=f"Tool '{tool}' is not allow-listed")
        method, path = ALLOWED_TOOLS[tool]
        try:
            path = path.format(**arguments)
        except KeyError as exc:
            return ToolResult(ok=False, error=f"Missing tool argument: {exc.args[0]}")
        headers = {"Authorization": f"Bearer {self.token}"} if self.token else {}
        payload = arguments.get("body")
        for attempt in range(self.retries + 1):
            try:
                async with httpx.AsyncClient(base_url=self.base_url, timeout=self.timeout) as client:
                    response = await client.request(method, path, json=payload, headers=headers)
                data = response.json() if response.content else None
                if response.is_success:
                    return ToolResult(ok=True, status_code=response.status_code, data=data)
                if response.status_code < 500:
                    return ToolResult(ok=False, status_code=response.status_code, data=data, error="Tool rejected request")
            except (httpx.TimeoutException, httpx.TransportError) as exc:
                if attempt == self.retries:
                    return ToolResult(ok=False, error=f"Tool unavailable: {type(exc).__name__}")
            await asyncio.sleep(0.1 * (2**attempt))
        return ToolResult(ok=False, error="Tool failed safely")
