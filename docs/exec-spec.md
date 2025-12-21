# Armada Executive Specification (Phased MVP, 12-Week First Playable)

## Vision & Pillars

- Turn-based 3v3 WEGO naval tactics on mobile; painterly early Age of Sail tone with light fantasy.
- Ship-first progression; captains with distinct abilities and crew-chain synergies.
- Cosmetic-first monetization (sails, liveries) to preserve fairness.
- Deterministic combat with authenticity cues: wind influence, broadsides, raking fire, boarding.

## Scope & Outcomes

- MVP (12 weeks, first-playable): core WEGO PvE loop, 5–8 handcrafted missions, ship upgrades/components, captain leveling + starter crew-chain bonus, sail cosmetics scaffolding, telemetry, anti-tamper basics, CI/CD and patch path.
- Post-MVP roadmap: co-op raids, async PvP defenses, seasonal live-ops cadence/events, richer crew chains + ultimates, battle pass, deeper platform services (social, guilds, payments optimization, anti-cheat hardening).
- Success for first-playable: stable on mid-tier devices (~30 fps target), crash rate <1%, session success 95%+, D1/D7 retention targets set per soft-launch market, 20–30 min day-1 engagement, cosmetic preview/equip working with stubbed payments in test env.

## Product Requirements (MVP)

- Core loop: simultaneous order planning → deterministic resolution; visible wind arrow impacting movement; broadsides as primary fire; raking-fire bonus from bow/stern arcs; boarding as limited special (risk/reward, cooldown).
- Combat rules: initiative from ship speed + captain bonus; collisions/friendly fire off for MVP; damage tracks (hull, sail, crew) with simple statuses (fire, slow); AI behaviors (aggressive, kiting, line-advance).
- Progression: ships are primary power—levels + component upgrades (hull, sails, cannons); captains level/rarity; crew-chain v0 grants small synergy when matched; resources: gold (soft), timber/ore (upgrade mats), shards (captain).
- Missions: 5–8 PvE scenarios introducing wind, broadsides, raking, boarding, boss ship; win/lose conditions and 3-star goals; repeatable with diminishing returns.
- Monetization (cosmetic-only): store scaffolding for sail skins; preview + equip; content via config; no power impact.
- Telemetry: session start/end, mission start/end, fail reasons, device/perf, economy sinks/sources, store impressions/clicks.
- Fairness/security baseline: authoritative rules, client tamper checks, HTTPS/TLS, basic replay validation.

## Technical Architecture (MVP)

- Client: Unity (iOS/Android), C#; deterministic combat sim module; WEGO state machine; addressable assets for ships/sails; balance via ScriptableObjects + remote JSON config.
- Backend: lightweight services (Node/TypeScript or Go) behind API gateway; auth (guest → platform ID), profiles, inventory/economy, missions; content/feature-flag service (remote config); match shell stub for future async PvP; telemetry ingestion → warehouse.
- Data: Postgres (players, inventory, missions, config versions), Redis (sessions/flags cache), object storage (sail textures, patches), CDN for asset delivery.
- Deployment: CI/CD with build + unit tests + static analysis + device-farm smoke; envs dev/stage/prod; blue/green for backend; crash/perf monitoring (Unity diagnostics + backend APM); feature flags for safely hiding incomplete features.
- Security: signed requests with short-lived tokens, server-side economy validation, minimal PII, rate limiting, WAF.

## Delivery Plan (12 Weeks, Critical Path)

- W1–2 Foundations: finalize PRD; choose engine/SDKs; repo + CI; backend scaffold (auth/profile/inventory); combat sim skeleton; art style guide + placeholders.
- W3–4 Core Combat: wind, movement, firing arcs, raking bonus, boarding special; AI behaviors; VFX/SFX placeholders; missions 1–3 blockout; economy schemas.
- W5–6 Progression & Missions: ship upgrades/components; captain leveling + basic crew-chain bonus; resource loops; missions 4–6 + boss v0; telemetry events; config/flag service.
- W7–8 Cosmetics & Polish: sail cosmetics pipeline (upload → CDN → preview/equip); UI polish; performance passes; missions 7–8 polish.
- W9–10 QA/Perf Prep: device matrix testing; crash/perf fixes; tutorialization; test payments stub; go/no-go gate on stability/engagement for test cohort.
- W11 Content Lock: blockers only; localization stub; store content pack 1; analytics dashboards.
- W12 Soft Launch: submit builds to limited region; live dashboards; patch readiness.
- Gates: G1 end W2 (sim loop viable), G2 end W6 (full loop playable), G3 end W10 (stability/perf), Launch end W12 (go).

## Org & Budget

- Team: Eng Lead; 2–3 Client Eng; 1 Backend Eng; 1 Tech Art/Tools; 1–2 Designers (systems/levels); 1 PM/Producer; 1 QA lead + vendor QA; part-time Audio; part-time LiveOps analyst.
- RACI: Eng Lead (A) tech; PM (A) scope/schedule; Design (A) gameplay; QA (A) quality gates.
- Costs: CI runners; device farm credits; APM/logging; CDN/storage; DB/Redis small prod cluster; art tools; vendor QA budget. Sized for lean 12w vertical slice with soft-launch operations.

## Risks & Mitigations

- Determinism/correctness: sim harness + replay validation; lock-step tests in CI.
- Mobile performance: weekly profiling on target devices; strict VFX/poly budgets; LOD fallbacks.
- Scope creep: cap missions at 8; lock at W11; feature flags to hide incomplete modes.
- Cheating/abuse: server-validated economy; signed configs; replay sanity checks; device integrity signals where available.
- Timeline compression: protect critical path (sim, missions, progression); de-scope cosmetics variety if needed; vendor QA to parallelize.
- Live-ops readiness: remote config + A/B hooks in MVP; defer raids/PvP until post-MVP data.

## Roadmap Notes (Post-MVP)

- Co-op raids with scalable HP pools and community milestones.
- Async PvP defenses with replay review and ladder seasons.
- Seasonal live-ops cadence (monthly themes, events, cosmetic drops).
- Battle pass aligned to events; deeper crew-chain ultimates; expanded cosmetics (figureheads, trails, ports).
