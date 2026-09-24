#!/usr/bin/env python3
"""Scores the UI usability survey (Docs/USABILITY-SURVEY.md) against proposal Table 5 (85%+ positive)."""
import csv
import sys

QUESTIONS = [f"q{i}" for i in range(1, 11)]
TASKS = [f"t{i}" for i in range(1, 7)]
TARGET = 0.85


def main(path):
    with open(path, newline="", encoding="utf-8") as handle:
        rows = [r for r in csv.DictReader(handle) if any(r.get(q, "").strip() for q in QUESTIONS)]
    if not rows:
        sys.exit("no answers in " + path)

    positive = 0
    for row in rows:
        ratings = [int(row[q]) for q in QUESTIONS if row.get(q, "").strip()]
        if sum(ratings) / len(ratings) >= 4.0:
            positive += 1

    share = positive / len(rows)
    print(f"respondents: {len(rows)}")
    print(f"positive (average >= 4.0): {positive} = {share:.0%}  target {TARGET:.0%}  -> {'PASS' if share >= TARGET else 'FAIL'}")
    print("per question, share answering 4 or 5:")
    for q in QUESTIONS:
        answered = [int(r[q]) for r in rows if r.get(q, "").strip()]
        agree = sum(1 for a in answered if a >= 4) / len(answered)
        print(f"  {q}: {agree:.0%}{'  <- below target' if agree < TARGET else ''}")
    print("tasks completed unaided:")
    for t in TASKS:
        done = [r[t] for r in rows if r.get(t, "").strip()]
        if done:
            print(f"  {t}: {sum(1 for d in done if d.strip() == '1') / len(done):.0%}")
    if len(rows) < 20:
        print("warning: fewer than 20 respondents; the percentage is not meaningful yet")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "Tools/qa/survey-template.csv")
