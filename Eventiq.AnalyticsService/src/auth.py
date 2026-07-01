"""JWT extraction for the analytics API.

Requests arrive through the Ocelot gateway, which already validates the RSA
JWT. We re-read the token here to learn *who* is asking (role + orgId) so the
endpoint can branch: admins get full Text2SQL, orgs get the scoped pipeline.

If `JWT_PUBLIC_KEY_PATH` is set we verify the signature (RS256, defence in
depth); otherwise we trust the gateway's validation and decode without it. The
token is minted by UserService with `MapInboundClaims` cleared, so claim keys
are the raw values below (role under the ClaimTypes.Role URI, `orgId` literal).
"""
from __future__ import annotations

import logging
import os
from functools import lru_cache

import jwt

logger = logging.getLogger("analytics-auth")

_ROLE_URI = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
_ISSUER = "eventiq-auth"
_AUDIENCE = "eventiq"

ADMIN = "Admin"
ORGANIZATION = "Organization"
STAFF = "Staff"


class AuthError(Exception):
    """Raised when a token is missing or invalid."""


@lru_cache(maxsize=1)
def _public_key() -> str | None:
    path = os.getenv("JWT_PUBLIC_KEY_PATH")
    if not path:
        return None
    try:
        with open(path, "r", encoding="utf-8") as f:
            return f.read()
    except OSError as e:
        logger.warning("Cannot read JWT_PUBLIC_KEY_PATH (%s): %s", path, e)
        return None


def _decode(token: str) -> dict:
    key = _public_key()
    if key:
        return jwt.decode(
            token, key, algorithms=["RS256"],
            issuer=_ISSUER, audience=_AUDIENCE,
        )
    # No key configured — trust the gateway, just read the claims.
    return jwt.decode(token, options={"verify_signature": False})


def principal_from_header(authorization: str | None) -> dict:
    """Parse `Authorization: Bearer <jwt>` → {user_id, role, org_id}.

    Raises AuthError if the header is missing/malformed or the token is bad.
    """
    if not authorization or not authorization.lower().startswith("bearer "):
        raise AuthError("Missing or malformed Authorization header")
    token = authorization.split(" ", 1)[1].strip()
    try:
        claims = _decode(token)
    except jwt.PyJWTError as e:
        raise AuthError(f"Invalid token: {e}") from e

    return {
        "user_id": claims.get("sub"),
        "role": claims.get(_ROLE_URI) or claims.get("role"),
        "org_id": claims.get("orgId"),
        "email": claims.get("email"),
    }
