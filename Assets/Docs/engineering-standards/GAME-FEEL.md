# Game Feel & UX Craft — the design-side standards

How the feel and teaching layers of a casual game get engineered — the design-facing companion to
[`PERFORMANCE.md`](PERFORMANCE.md) (which budgets what these effects may cost) and
[`ARCHITECTURE.md`](ARCHITECTURE.md) §8 (the sim/view split that makes responsive feel possible at
all). The quality bar throughout: what a top-tier casual studio (Royal Match / Toon Blast class)
would ship. Rules here were distilled from studying those references and from shipped iterations —
treat every one as a **design default to validate by playtest** (an observed pattern or a
hypothesis with rationale), never as an engine fact or a proven genre law. Last reviewed
2026-08-06. Sections carrying figures rest on **project measurement of one 2D casual game plus its
references**, stated in place — calibrated starting points to re-measure against your own
references, never genre constants.

---

## 1. Research before building — the reference-pattern rule

Before implementing any player-facing UX feature (tutorial, reward flow, fail flow, input scheme),
**study how the top 2–3 games of the genre present it and write the observed pattern down** — where
things sit on screen, what animates, what the player may touch, how it dismisses. Building from
imagination first and "polishing later" reliably produces layouts and interactions that read as
amateur next to the references; two shipped iterations of guesswork cost more than one afternoon of
study. The written pattern also becomes the review yardstick: "does ours read like the reference?"
is answerable.

**Measure the reference, don't eyeball it.** "Study" means numbers, and a screen capture plus
`ffmpeg` is the whole toolchain: pull the clip to frames, find the moving element and the window it
moves in, crop per frame, and read off the quantities that actually drive the feel — duration in
frames, oscillation frequency in Hz, peak angle in degrees, travel speed in *screen* units. Eyeball
comparison consistently reports "close enough" for motion that is twice too fast, because the eye
judges the envelope and not the rate.

**A deliberate deviation from the measured reference is recorded, not absorbed.** When you knowingly
run slower, longer or softer than the reference for readability, write the reason **next to the
number** — and update it when the reason changes, or the next reader finds a value contradicting the
text beside it and trusts the text. Deviations that go unrecorded stop being decisions and become
folklore within one sprint.

## 2. Tutorial / FTUE engineering

The pattern the genre leaders converge on (dim-and-spotlight, in-board, no modal):

- **Dim the world, keep the subject lit.** A full-screen dim with the lesson's tiles kept at full
  brightness above it — the subject AND the tile the enabling move touches (subject alone leaves
  the pointing hand ambiguous). One representative tile beats lighting every match: focus teaches;
  a lit-up crowd doesn't.
- **The explanation is a tile-anchored bubble, one sentence, ≤10 words**, verb-first, naming the
  mechanic — never a center modal, never a fixed bottom bar, no title line, no mascot in-board.
  Flip the bubble above/below the subject by available space.
- **Dismissal is the guided first tap, not a button.** While dimmed, only the correct move
  registers (the rest of the board and UI are input-locked); the tap dismisses the lesson AND plays
  as the first real move — the mechanic firing *is* the explanation. A "Got it" button is a dead
  tap that delays the payoff. Indirect mechanics (where the obstacle itself isn't tappable) get a
  **designer-authored enabling move** in the level data, so the lesson still ends with the
  mechanic visibly firing.
- **The hand cursor appears only when a tap is wanted** — pressing on the exact target, with a
  small expand-and-fade ripple at the contact point. A hand on a passive "look at this" beat
  teaches players to wait for hands.
- **One new mechanic per level, on a deliberately easy board** — and the lesson tap must NOT be
  the winning move: leave at least one free move after it, so the player *plays* what they just
  learned. Introductions are staged at level start after a short settle beat (~0.5 s), not as a
  pre-level popup.
- **Persistence contract:** seen-flags are stored by stable NAME (`CONVENTIONS.md` §6) and written
  **on dismissal, never on show** — an app killed mid-lesson replays it next launch; a completed
  lesson never re-triggers, including across level loops.

## 3. Motion is authored, not simulated

- **Gravity, falls, and bounces in a grid game are analytic curves** (accelerating fall, small
  squash/settle on land, per-column stagger) driven by one clocked update — never `Rigidbody`
  physics, which is nondeterministic, fights the sim/view split, and reads as a red flag in review.
  Physics engines are for physics games.
- **The view animates toward an already-final state** (`ARCHITECTURE.md` §8): animation timing is a
  presentation choice that can be tuned freely — or skipped entirely under fast-forward — without
  touching rules.
- **Calibrate motion in screen-space, not seconds.** The same cadence reads differently at
  different element sizes; when matching a reference, match the *on-screen speed* of the moving
  front, and keep durations tunable in the Inspector during Play (`[SerializeField]`, baked back
  into defaults once liked).
- **Tween discipline is `PERFORMANCE.md` §7**: starting a tween is an event; lifetime-linked;
  unscaled time through pauses. One clocked driver for N elements is §4's rule there, restated
  below.
- **Continuously-retargeted motion wants an integrator, not a tween — an ownership argument, not
  an API gap.** When a target can change mid-flight (a falling block whose destination drops
  further because blocks beneath it were blasted), a hand-rolled integrator
  (`velocity += g·dt; position += velocity·dt; settle at current target`) retargets for free: the
  target is just a variable the sim updates, and one class owns all the state. Be accurate about
  the alternative: DOTween *can* retarget supported tweeners (`ChangeEndValue` — with restrictions
  inside sequences and for some plugin types) and *can* run on a manually-stepped clock
  (`UpdateType.Manual` + `DOTween.ManualUpdate`), so the preference does not rest on missing APIs.
  It rests on what high-count retargetable motion does to tween code — per-element tween objects,
  duration re-math on every change, `Kill`/`OnComplete` interaction paths to test — versus one
  integrator with explicit state; and on measured cost (`PERFORMANCE.md` §4's per-element rule).
  Use tweens for cold, one-shot, fixed-target motion; use a clocked driver for hot, retargetable,
  N-element motion.
- **When a value "does nothing", check the transfer function before tuning the value.** The
  authored number is often never reached, because something between it and the effect scales it
  down. Measured instance: a landing beat scaled its strength linearly by `speed / maxSpeed`, so an
  ordinary drop arrived at 45% of the ceiling and applied 4.5% of the authored squash — under five
  pixels, invisible, and *no amount of tuning that number* would have fixed it. The fix is an
  authored response curve whose left end lifts off zero, not a bigger constant.
  The same trap has a second form: the knob that looks like the answer often compresses the wrong
  part. Raising an easing exponent to "slow the settle down" pulls everything toward the start and
  makes the flick *faster*; what actually worked was moving the beat's position along the curve.
  **Ask what the number multiplies before you change it.**
- **Direction changes have a legibility ceiling: around 8 Hz reads as a swing, around 12 Hz reads
  as a buzz.** Measured twice independently here — once matching a reference's invalid-input wiggle
  (~0.18–0.2 s at ~8 Hz, first tilt ~12°, against ours at 12 Hz which read as vibration), and again
  when a decaying second bounce was added to a landing and cut after playing it, because two full
  swings put four direction changes at ~8.8 Hz. Two oscillations is usually the budget; a third
  reads as a rattle, and references generally show one compression and one recovery, not a train.
- **Mass reads through contrast, not through depth.** Making every impact heavier makes none of
  them feel heavy — a short drop has to stay light or the whole scene goes uniform, so the
  impact-by-speed response needs a genuinely low left end, not a comfortable floor. The other half
  is the fall itself: stretch past roughly 1.15 reads as a droplet rather than a stone, and no
  amount of landing work rescues a fall that already looks like jelly.
- **Hand-rolled ≠ unreadable — the player-class pattern.** The readability of `DOMove(...)` in one
  line comes from its API, not its library, and the same surface is available by hand: each motion
  concern is its **own plain-C# player class** (`FallAnimator`, `BlastFxPlayer` — single
  responsibility, no `MonoBehaviour`) with a declarative one-line entry (`Begin(view, target)`)
  and a `Step(dt)` the owning view calls from a one-line `Update`. Easing lives in named functions
  or `AnimationCurve` fields, so behaviour changes are data changes. This keeps call sites as
  short as a tween call, makes the driver trivially steppable in tests (`Step(0.016f)` — DOTween
  needs its manual-update mode and ownership of a global clock for the same), and contains growing
  retarget/cancel complexity inside one class instead of a web of `Kill`/`OnComplete` callbacks.
  What this pattern forbids is the naive version: interpolation soup inlined into a controller's
  `Update`.

## 4. Input feel

- **Act on touch-began, not touch-up**, for board taps — the perceived snappiness difference is
  large, and it is the pattern the studied genre references converge on.
- **Grid hit-testing is math, not physics**: pointer → cell by coordinate arithmetic, no colliders,
  no raycasts (convention, not engine doctrine — the wins are zero physics cost and a strippable
  physics module).
- **Never lock input as an animation side effect.** Invalid taps no-op; valid taps on stationary
  content work while other content animates (the sim/view split makes this free). Input locks are
  explicit, named flow states (a win/fail sequence, a tutorial) with a single owner — and a lock
  is expressed at the input boundary (raycaster off, one gate in the handler), not sprinkled
  through views.
- **Input-during-animation is a per-mechanic policy, not a universal rule.** Instant logical
  commit + visual replay (**Accept**) is the right default for the puzzle core — but interacting
  with a visibly moving object can be genuinely ambiguous, so each mechanic picks one policy
  deliberately: **Accept** (apply to the logical state now), **Queue** (order-preserve, apply
  after the transition), **Merge** (fold the command into the active transition), or **Gate**
  (reject/defer until a safe point, with visible feedback). The choice answers: what does the
  player visually target; can logical and visual identity diverge; do commands commute; can a
  queued command become invalid; what does a rejected input show?
- **Generous hit targets**; on the palette/buttons, pressed-state feedback is scale squash, not
  tint (tint reads as cheap).

## 5. Juice, budgeted

Feel is visual + haptic + audio together, and every channel has a budget:

- **Per-event effect budget** written next to the effect: particle counts, `maxParticles`, screen
  shake ≤0.1 s and only on big events, one haptic tick per meaningful event (light/medium/heavy
  mapped to a deliberate scale — paint, break, win). Juice that breaks the frame budget
  (`PERFORMANCE.md` §3) isn't polish; it's a regression.
- **Celebrate on the reward moment, not on every action** — constant fireworks flatten the curve
  that makes the big moment land.
- **Signature moments get engineered depth** (the fold, the cascade, the win sweep): these are the
  moments worth a measured animation-quality bar (a written, checkable barrier — timing curves,
  overlap, easing — the equivalent of a "does it read as real material?" test), because they carry
  the game's identity.
- **An effect earns its place by being seen in play, not by being reasoned about.** Effects that
  are obviously right on paper routinely ship as working code and come straight back out: a landing
  dust the reference does not have, a hit-flash invisible because a multiply-only shader can only
  darken and the sprite swap in the same frame hid it, a second bounce that read as jitter. Budget
  for building effects you will delete — and when you delete one, `CONVENTIONS.md` §5's rule
  applies: pin the absence with a test if the removal was a decision.
- **Effect parameters live next to the thing they belong to, and inherit its timing.** The particle
  budget (`PERFORMANCE.md` §4a) is written on the prefab; the colour a burst tints to belongs beside
  the art it was sampled from, not in the effect system; and an effect that accompanies an event
  rides that event's stagger rather than inventing its own clock. Effects that keep private
  timelines drift out of sync with the thing they are decorating the first time the thing is
  retuned.

## 6. Content difficulty is a regression gate, not a memory

Authored content decays silently: a level that was fair when it was tuned becomes unwinnable three
balance changes later, and nobody notices until a player reports it. Automate the floor.

- **A goal-seeking bot plays every shipped level N times** (with the refill/randomness seeds that
  real attempts would use) and the suite **fails below a win-rate floor**. This turns "is the
  content still beatable?" from a memory into a gate. Measured value here: four shipped levels came
  back below the floor — **two of them at a 0% win rate** over 200 seeds each — which ordinary
  playtesting had noticed as "level 3 is hard" without isolating.
- **Read the bot's numbers as a floor, not a forecast.** A one-ply planner is worse than a human, so
  its win rate under-reports the real one; the useful signal is *relative* and the direction of
  change, not the absolute figure.
- Three findings that generalise across tile-matching games, all from that sweep: **the
  colour/token count dominates difficulty** (at K kinds, a colour-collection goal asks for a 1/K
  share of the board, so raising K quietly multiplies the requirement); **an obstacle adjacent to
  another obstacle in a corner is a trap**, because it leaves a single live cell to act from; and
  **difficulty is a property of the (layout, seed) pair**, not the layout — the authored seed
  decides the opening every attempt starts from, so sweeping seeds is part of tuning a level, not
  an afterthought.

## 7. Ship the feel with proof

Polish claims follow the same rule as performance claims (`PERFORMANCE.md` §11): "the tutorial
matches the reference pattern" is demonstrated with a side-by-side capture; "the cascade feels
right" has a device capture at target frame rate. What can't be measured is still *reviewable* —
record it and look.
