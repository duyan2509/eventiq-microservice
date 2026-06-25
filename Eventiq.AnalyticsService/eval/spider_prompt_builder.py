"""Build the SQL generation prompt for Spider (English, SQLite).

Adapted from src/prompt_builder.py with:
- English instructions
- SQLite-specific syntax notes
- No Eventiq naming conventions
"""
from __future__ import annotations

_FEW_SHOT = """\
Q: How many singers do we have?
A: SELECT count(*) FROM singer;

Q: Show all countries where a singer older than 20 comes from.
A: SELECT DISTINCT country FROM singer WHERE age > 20;

Q: List concert names and the stadium name they are held in, ordered by year.
A: SELECT T1.concert_name, T2.name FROM concert AS T1 JOIN stadium AS T2 ON T1.stadium_id = T2.stadium_id ORDER BY T1.year;

Q: What is the average capacity of stadiums that have hosted more than 1 concert?
A: SELECT AVG(T1.capacity) FROM stadium AS T1 JOIN concert AS T2 ON T1.stadium_id = T2.stadium_id GROUP BY T1.stadium_id HAVING count(*) > 1;"""


def build_prompt(question: str, subgraph: dict, ddl_map: dict[str, str]) -> str:
    """Assemble the full SQL generation prompt.

    Parameters
    ----------
    question : str
        English natural language question
    subgraph : dict
        Output from schema linking: {"tables": [...], "join_hints": [...]}
    ddl_map : dict
        table_name → CREATE TABLE statement (only linked tables)
    """
    ddl_section = "\n\n".join(ddl_map.values()) if ddl_map else "(no schema available)"

    join_hints = subgraph.get("join_hints", [])
    if join_hints:
        join_section = "\n".join(f"  {h}" for h in join_hints)
    else:
        join_section = "  (use JOIN conditions from FOREIGN KEY constraints above)"

    return f"""You are a SQLite expert. Write a single SQL query to answer the question.
Return ONLY the SQL query ending with a semicolon. No explanation, no markdown, no ```sql fences.

-- Database schema (SQLite)
{ddl_section}

-- Relevant JOIN conditions
{join_section}

-- SQLite syntax rules
- Use LIMIT instead of TOP or FETCH FIRST
- Use strftime() for date functions, not DATE_TRUNC
- Column names may be mixed-case; match them exactly as shown in the schema
- Use table aliases (T1, T2, ...) when joining multiple tables

-- Examples
{_FEW_SHOT}

-- Question
Q: {question}
A:"""
