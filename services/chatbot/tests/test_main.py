import unittest

from services.chatbot.app.main import answer


class ChatbotAnswerTests(unittest.TestCase):
    def test_greeting(self) -> None:
        self.assertIn("Hello", answer("hello").reply)

    def test_account_question(self) -> None:
        self.assertIn("Register", answer("How do I create an account?").reply)

    def test_coverage_question(self) -> None:
        self.assertIn("Coverage", answer("How do I get insurance?").reply)

    def test_claim_question(self) -> None:
        self.assertIn("Claims", answer("How do I file a claim?").reply)


if __name__ == "__main__":
    unittest.main()
