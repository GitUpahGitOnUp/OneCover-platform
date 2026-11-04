from __future__ import annotations

from dataclasses import dataclass
from typing import Final

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel, Field


@dataclass(frozen=True)
class ChatReply:
    reply: str
    suggestions: tuple[str, ...] = ()


class ChatRequest(BaseModel):
    message: str = Field(min_length=1, max_length=500)


class ChatResponse(BaseModel):
    reply: str
    suggestions: list[str]


GREETING_WORDS: Final = {"hi", "hello", "hey", "greetings", "good morning", "good afternoon"}
DEFAULT_SUGGESTIONS: Final = (
    "How do I create an account?",
    "How do I get coverage?",
    "How do I file a claim?",
)


def answer(message: str) -> ChatReply:
    """Return a safe, product-specific answer without inventing account data."""
    normalized = " ".join(message.lower().strip().split())

    if normalized in GREETING_WORDS or any(normalized.startswith(f"{word} ") for word in GREETING_WORDS):
        return ChatReply(
            "Hello! I can help you navigate OneCover, create an account, get coverage, or file a claim.",
            DEFAULT_SUGGESTIONS,
        )
    if any(term in normalized for term in ("create an account", "register", "sign up", "new account")):
        return ChatReply(
            "Select Get started or Register, enter your email and password, then provide the requested member details. After registration, you can sign in and access your dashboard.",
            ("Where is the sign-in page?", "How do I get coverage?"),
        )
    if any(term in normalized for term in ("sign in", "login", "log in", "password")):
        return ChatReply(
            "Choose Sign in and enter your account email and password. If you were redirected from another page, OneCover returns you there after a successful sign-in.",
            ("How do I create an account?", "What is on my dashboard?"),
        )
    if any(term in normalized for term in ("coverage", "insurance", "plan", "enroll", "enrollment")):
        return ChatReply(
            "Open Coverage to browse available plans, compare tiers and premiums, and enroll. You must be signed in with a member account to enroll. Your active enrollment appears on the Coverage page.",
            ("How do I file a claim?", "What does my profile show?"),
        )
    if any(term in normalized for term in ("claim", "leave", "disability", "file")):
        return ChatReply(
            "Open Claims and choose File a claim. Select active STD or LTD coverage, enter the reason and leave dates, then submit. You can return to Claims to follow the status and history of your claim.",
            ("What coverage can I use for a claim?", "How do I get coverage?"),
        )
    if any(term in normalized for term in ("profile", "fmla", "dashboard", "navigate", "where")):
        return ChatReply(
            "Use the top navigation to open Home, Profile, Coverage, or Claims. Profile shows your account and employment details, while Home links to the main member actions.",
            DEFAULT_SUGGESTIONS,
        )
    return ChatReply(
        "I can help with navigating OneCover, creating an account, getting coverage, or filing a claim. Try asking about one of those topics.",
        DEFAULT_SUGGESTIONS,
    )


app = FastAPI(title="OneCover Chatbot", version="1.0.0")
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["GET", "POST"],
    allow_headers=["*"],
)


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "Healthy"}


@app.post("/api/chatbot/message", response_model=ChatResponse)
def chat(request: ChatRequest) -> ChatResponse:
    result = answer(request.message)
    return ChatResponse(reply=result.reply, suggestions=list(result.suggestions))
