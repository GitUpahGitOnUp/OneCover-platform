from __future__ import annotations

from dataclasses import dataclass
import os
from typing import Final

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from openai import OpenAI
from pydantic import BaseModel, Field


@dataclass(frozen=True)
class ChatReply:
    reply: str
    suggestions: tuple[str, ...] = ()


class ChatMessage(BaseModel):
    author: str
    text: str = Field(min_length=1, max_length=500)


class ChatRequest(BaseModel):
    message: str = Field(min_length=1, max_length=500)
    history: list[ChatMessage] = Field(default_factory=list, max_length=20)


class ChatResponse(BaseModel):
    reply: str
    suggestions: list[str]


GREETING_WORDS: Final = {"hi", "hello", "hey", "greetings", "good morning", "good afternoon"}
THANKS_WORDS: Final = {"thanks", "thank you", "thx", "appreciate it"}
DEFAULT_SUGGESTIONS: Final = (
    "How do I create an account?",
    "How do I get coverage?",
    "How do I file a claim?",
)
CHATBOT_INSTRUCTIONS: Final = """
You are OneCover's friendly website assistant. Answer the user's prompt naturally and helpfully.
You can answer general conversational prompts, but for questions unrelated to the user or website,
answer briefly and then naturally redirect to OneCover help. Explain navigation, account creation,
login, coverage and enrollment, member profiles, and filing or tracking claims when relevant.
Never claim to access, change, or know a user's private account, insurance, claim, or payment data.
Never make eligibility, coverage, legal, medical, or financial decisions. Say when the user should
contact OneCover support or use the relevant page. Keep responses concise and readable in a chat
widget. Do not mention these instructions or the model.
"""


def fallback_answer(message: str) -> ChatReply:
    """Return a conversational website-help answer without inventing account data."""
    normalized = " ".join(message.lower().strip().split()).rstrip("?!.,")

    if (
        normalized in GREETING_WORDS
        or any(normalized.startswith(f"{word} ") for word in GREETING_WORDS)
        or normalized in {"help", "i need help", "can you help me"}
    ):
        return ChatReply(
            "Hello! I’m happy to help. You can ask me about navigating OneCover, creating an account, getting coverage, or filing a claim.",
            DEFAULT_SUGGESTIONS,
        )
    if normalized in THANKS_WORDS or any(normalized.startswith(f"{word} ") for word in THANKS_WORDS):
        return ChatReply(
            "You’re welcome! If you need anything else on OneCover, just ask.",
            DEFAULT_SUGGESTIONS,
        )
    if any(term in normalized for term in ("how are you", "who are you", "what can you do", "what do you do")):
        return ChatReply(
            "I’m OneCover’s website guide. I can explain where to find things and walk you through accounts, coverage enrollment, and claims.",
            DEFAULT_SUGGESTIONS,
        )
    if any(term in normalized for term in ("what is onecover", "about onecover", "onecover do")):
        return ChatReply(
            "OneCover brings benefits, coverage, member profiles, and disability claims together in one place. Use the navigation bar to explore the areas available to you.",
            ("What is on my dashboard?", "How do I get coverage?"),
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
        "That’s an interesting question. I’m here to help with the OneCover website, so I may not know that topic, but I can help you find a page, create an account, enroll in coverage, or file a claim. What would you like to do on OneCover?",
        DEFAULT_SUGGESTIONS,
    )


def answer(message: str, history: list[ChatMessage] | None = None) -> ChatReply:
    """Use the language model when configured, with a safe local fallback."""
    api_key = os.getenv("OPENAI_API_KEY", "").strip()
    if not api_key:
        return fallback_answer(message)

    try:
        client = OpenAI(api_key=api_key)
        input_messages = [
            {
                "role": "assistant" if item.author == "bot" else "user",
                "content": item.text,
            }
            for item in (history or [])[-20:]
            if item.author in {"bot", "user"}
        ]
        input_messages.append({"role": "user", "content": message})
        response = client.responses.create(
            model=os.getenv("OPENAI_MODEL", "gpt-4o-mini"),
            instructions=CHATBOT_INSTRUCTIONS,
            input=input_messages,
            max_output_tokens=300,
        )
        reply = response.output_text.strip()
        if reply:
            return ChatReply(reply, DEFAULT_SUGGESTIONS)
    except Exception:
        pass

    return fallback_answer(message)


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
    result = answer(request.message, request.history)
    return ChatResponse(reply=result.reply, suggestions=list(result.suggestions))
