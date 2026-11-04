import os
import unittest
from unittest.mock import patch

from services.chatbot.app.main import ChatMessage, answer


class ChatbotAnswerTests(unittest.TestCase):
    def test_greeting(self) -> None:
        self.assertIn("Hello", answer("hello").reply)

    def test_account_question(self) -> None:
        self.assertIn("Register", answer("How do I create an account?").reply)

    def test_coverage_question(self) -> None:
        self.assertIn("Coverage", answer("How do I get insurance?").reply)

    def test_claim_question(self) -> None:
        self.assertIn("Claims", answer("How do I file a claim?").reply)

    def test_conversational_prompt(self) -> None:
        self.assertIn("website guide", answer("What can you do?").reply)

    def test_out_of_scope_prompt_redirects_naturally(self) -> None:
        reply = answer("What is the best movie this year?").reply
        self.assertIn("interesting question", reply)
        self.assertIn("OneCover website", reply)

    def test_fallback_is_used_without_api_key(self) -> None:
        self.assertIn("website", answer("Tell me a joke").reply)

    def test_history_message_contract(self) -> None:
        self.assertEqual(ChatMessage(author="user", text="hello").author, "user")

    @patch.dict(os.environ, {"GEMINI_API_KEY": "test-key", "GEMINI_MODEL": "test-model"})
    @patch("services.chatbot.app.main.genai.Client")
    def test_model_path_sends_prompt_and_history(self, client_mock) -> None:
        client_mock.return_value.models.generate_content.return_value.text = "A generated answer."

        result = answer("What should I do next?", [ChatMessage(author="user", text="Hello")])

        self.assertEqual(result.reply, "A generated answer.")
        call = client_mock.return_value.models.generate_content.call_args.kwargs
        self.assertEqual(call["model"], "test-model")
        self.assertIn("User: What should I do next?", call["contents"])
        self.assertIn("User: Hello", call["contents"])


if __name__ == "__main__":
    unittest.main()
