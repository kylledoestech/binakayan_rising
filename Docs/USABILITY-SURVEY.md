# UI usability survey (#53)

Proposal Table 5 target: **85% or more of respondents give positive feedback on UI clarity.**
This page is the whole kit: the protocol, the form, and the scoring. Give testers the form as a
Google Form or on paper, export the answers to CSV in the shape of
[`Tools/qa/survey-template.csv`](../Tools/qa/survey-template.csv), then run:

```bash
python3 Tools/qa/survey-score.py answers.csv
```

## What counts as "positive"

A respondent is **positive** when the average of their ten ratings is **4.0 or higher** on the
1–5 scale. The script also reports, for each question, the share of respondents who answered 4 or 5,
so a weak screen is visible even if the overall target is met. **Pass** means at least 85% of
respondents are positive.

## Protocol (about 20 minutes per tester)

1. Fresh install of the release build, with a new save and the language the tester prefers.
2. Say only: "Play as you normally would. Think aloud if you like. I can't help you." Don't
   explain the UI. Observer notes go in the `notes` column.
3. Tasks, in order. Tick each one the tester completes without help (`t1`–`t6`):
   1. Start a new campaign and finish the first camp task (q01).
   2. Deploy a squad and win the tutorial battle (q02).
   3. Answer the mid-battle question and pick a Tactician's Command.
   4. Train one unit at the Training Grounds.
   5. Open the Library and read one lesson.
   6. Pause a battle and change the music volume.
4. Hand over the form below. It is anonymous, so no names.

Aim for at least 20 testers from the target audience (Grade 9–12 or first-year college students);
with fewer, the percentage is not meaningful.

## The form

Rate each statement from 1 (strongly disagree) to 5 (strongly agree). Filipino in italics.

| # | Statement |
| --- | --- |
| q1 | I always knew what to do next. / *Lagi kong alam ang susunod kong gagawin.* |
| q2 | The buttons and labels were easy to read. / *Madaling basahin ang mga button at label.* |
| q3 | Placing units on the board was easy. / *Madali ang paglalagay ng mga yunit sa board.* |
| q4 | During a battle I could follow what was happening. / *Nasundan ko ang nangyayari sa labanan.* |
| q5 | The health bars, damage numbers and minimap were clear. / *Malinaw ang health bar, numero ng pinsala at minimap.* |
| q6 | The camp buildings were easy to find and use. / *Madaling hanapin at gamitin ang mga gusali sa kampo.* |
| q7 | The quiz questions were clear and fair. / *Malinaw at patas ang mga tanong sa pagsusulit.* |
| q8 | I could tell how much Reales, Rations and Scrap I had. / *Alam ko kung ilan ang aking Reales, Rasyon at Scrap.* |
| q9 | The text was easy to understand in my language. / *Madaling maintindihan ang teksto sa aking wika.* |
| q10 | Overall, the game's screens were clear and easy to use. / *Sa kabuuan, malinaw at madaling gamitin ang mga screen.* |
| open | What confused you the most? / *Ano ang pinakanakalito sa iyo?* |

## Results

_Not run yet. Paste the script's output here, with the date and the number of testers._
