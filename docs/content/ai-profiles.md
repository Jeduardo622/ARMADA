# AI Profiles (MVP)

Define reusable AI behaviors with tunable params so missions stay consistent.

## Profiles
- Aggressive: closes to optimal broadside range; favors raking if possible; risk-tolerant.
  - Params: preferred_range, rake_bias, retreat_threshold (low), focus_fire (high).
- Kiting: maintains standoff; avoids close range; uses wind to disengage.
  - Params: preferred_range (long), kite_bias, retreat_threshold (medium), focus_fire (medium).
- Line-advance: holds formation line, advances steadily, prioritizes staying broadside-aligned.
  - Params: formation_keep, rake_bias (low), retreat_threshold (low), focus_fire (medium).
- Boss (template): scripted phases; can switch between profiles; has specials on HP thresholds.
  - Params: phases[], specials[], enrage_threshold.

## Tuning Notes
- Raking priority should consider risk (exposure to counter-rake); keep per-profile bias defined.
- Retreat thresholds trigger repositioning, not flight; bosses may ignore retreat.
- Keep param defaults in config; missions override only when needed.

