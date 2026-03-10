import base64
import hashlib
import hmac
import os
import re
import secrets
import sqlite3
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from typing import Optional


EMAIL_REGEX = re.compile(r"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$")
PASSWORD_MIN_LENGTH = 12
PBKDF2_ITERATIONS = 600_000
PEPPER = os.getenv("APP_PASSWORD_PEPPER", "")


@dataclass
class SignupResult:
    user_id: int
    email: str
    verification_token: str


class AuthService:
    def __init__(self, db_path: str = "auth.db") -> None:
        self.conn = sqlite3.connect(db_path)
        self.conn.row_factory = sqlite3.Row
        self._init_db()

    def _init_db(self) -> None:
        self.conn.execute(
            """
            CREATE TABLE IF NOT EXISTS users (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                email TEXT UNIQUE NOT NULL,
                password_hash TEXT NOT NULL,
                password_salt TEXT NOT NULL,
                is_verified INTEGER NOT NULL DEFAULT 0,
                verification_hash TEXT,
                verification_expires_at TEXT,
                created_at TEXT NOT NULL
            )
            """
        )
        self.conn.commit()

    @staticmethod
    def _utc_now_iso() -> str:
        return datetime.now(timezone.utc).isoformat()

    @staticmethod
    def _normalize_email(email: str) -> str:
        return email.strip().lower()

    @staticmethod
    def _validate_email(email: str) -> bool:
        return bool(EMAIL_REGEX.match(email))

    @staticmethod
    def _validate_password(password: str) -> bool:
        if len(password) < PASSWORD_MIN_LENGTH:
            return False
        has_lower = any(c.islower() for c in password)
        has_upper = any(c.isupper() for c in password)
        has_digit = any(c.isdigit() for c in password)
        has_symbol = any(not c.isalnum() for c in password)
        return has_lower and has_upper and has_digit and has_symbol

    @staticmethod
    def _hash_token(token: str) -> str:
        return hashlib.sha256(token.encode("utf-8")).hexdigest()

    @staticmethod
    def _b64(data: bytes) -> str:
        return base64.b64encode(data).decode("utf-8")

    @staticmethod
    def _b64decode(data: str) -> bytes:
        return base64.b64decode(data.encode("utf-8"))

    def _hash_password(self, password: str, salt: Optional[bytes] = None) -> tuple[str, str]:
        if salt is None:
            salt = os.urandom(16)
        pwd = (password + PEPPER).encode("utf-8")
        digest = hashlib.pbkdf2_hmac("sha256", pwd, salt, PBKDF2_ITERATIONS)
        return self._b64(digest), self._b64(salt)

    def signup(self, email: str, password: str) -> SignupResult:
        email = self._normalize_email(email)
        if not self._validate_email(email):
            raise ValueError("Invalid email format.")
        if not self._validate_password(password):
            raise ValueError(
                "Weak password. Use 12+ chars with upper, lower, digit, and symbol."
            )

        pwd_hash, salt = self._hash_password(password)
        created_at = self._utc_now_iso()
        verification_token = secrets.token_urlsafe(32)
        verification_hash = self._hash_token(verification_token)
        expires_at = (datetime.now(timezone.utc) + timedelta(minutes=30)).isoformat()

        try:
            cur = self.conn.execute(
                """
                INSERT INTO users (
                    email, password_hash, password_salt, is_verified,
                    verification_hash, verification_expires_at, created_at
                )
                VALUES (?, ?, ?, 0, ?, ?, ?)
                """,
                (email, pwd_hash, salt, verification_hash, expires_at, created_at),
            )
            self.conn.commit()
        except sqlite3.IntegrityError as exc:
            raise ValueError("Email already exists.") from exc

        return SignupResult(
            user_id=cur.lastrowid,
            email=email,
            verification_token=verification_token,
        )

    def verify_email(self, token: str) -> bool:
        token_hash = self._hash_token(token)
        user = self.conn.execute(
            """
            SELECT id, verification_expires_at
            FROM users
            WHERE verification_hash = ? AND is_verified = 0
            """,
            (token_hash,),
        ).fetchone()
        if not user:
            return False

        expires_at = datetime.fromisoformat(user["verification_expires_at"])
        if datetime.now(timezone.utc) > expires_at:
            return False

        self.conn.execute(
            """
            UPDATE users
            SET is_verified = 1, verification_hash = NULL, verification_expires_at = NULL
            WHERE id = ?
            """,
            (user["id"],),
        )
        self.conn.commit()
        return True

    def login(self, email: str, password: str) -> bool:
        email = self._normalize_email(email)
        user = self.conn.execute(
            """
            SELECT password_hash, password_salt, is_verified
            FROM users
            WHERE email = ?
            """,
            (email,),
        ).fetchone()
        if not user:
            return False

        expected_hash = user["password_hash"]
        salt = self._b64decode(user["password_salt"])
        given_hash, _ = self._hash_password(password, salt)
        passwords_match = hmac.compare_digest(expected_hash, given_hash)
        return bool(passwords_match and user["is_verified"] == 1)


if __name__ == "__main__":
    auth = AuthService("auth.db")

    print("Creating account...")
    result = auth.signup("demo.user@example.com", "StrongPass!123")
    print(f"Created user id={result.user_id} email={result.email}")
    print(f"Verification token (send by email in real app): {result.verification_token}")

    print("\nTrying login before verification:")
    print("Login success:", auth.login("demo.user@example.com", "StrongPass!123"))

    print("\nVerifying email token:")
    print("Verify success:", auth.verify_email(result.verification_token))

    print("\nTrying login after verification:")
    print("Login success:", auth.login("demo.user@example.com", "StrongPass!123"))
