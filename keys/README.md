#  keys/

This folder contains **RSA keys**, which is used to
**sign and verofy JWT (RS256)** in Eventiq system.

---
## Key Generation
```bash
cd keys
openssl genrsa -out private.key 2048
openssl rsa -in private.key -pubout -out public.key
```
---
## Structure

```text
keys/
├── private.key   # RSA private key 
└── public.key    # RSA public key 
