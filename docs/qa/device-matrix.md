# Device Matrix (MVP Targets)

- Low-tier Android: e.g., Snapdragon 665, 4GB RAM, Android 11.
- Mid-tier Android: e.g., Snapdragon 778G, 6GB RAM, Android 13.
- iOS mid: e.g., iPhone 11/12 class.
- iOS low/mid: e.g., iPhone XR/SE(2).

Perf gates (guidance):
- FPS: target 30; p5 > 24 on low-tier; p50 > 30 on mid.
- Memory: < 800MB low-tier; < 1GB mid.
- Load: cold start < 12s; mission load < 6s.

Schedule:
- Weekly perf runs on matrix; gate at G2/G3; nightly smokes on representative mid device.

