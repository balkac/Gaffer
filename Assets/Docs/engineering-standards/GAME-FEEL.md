# Game Feel & UX Craft — the design-side standards

How the feel and teaching layers of a casual game get engineered — the design-facing companion to
[`PERFORMANCE.md`](PERFORMANCE.md) (which budgets what these effects may cost) and
[`ARCHITECTURE.md`](ARCHITECTURE.md) §8 (the sim/view split that makes responsive feel possible at
all). The quality bar throughout: what a top-tier casual studio (Royal Match / Toon Blast class)
would ship. Rules here were distilled from studying those references and from shipped iterations —
they are conventions with rationale, not engine facts.

---

## 1. Research before building — the reference-pattern rule

Before implementing any player-facing UX feature (tutorial, reward flow, fail flow, input scheme),
**study how the top 2–3 games of the genre present it and write the observed pattern down** — where
things sit on screen, what animates, what the player may touch, how it dismisses. Building from
imagination first and "polishing later" reliably produces layouts and interactions that read as
amateur next to the references; two shipped iterations of guesswork cost more than one afternoon of
study. The written pattern also becomes the review yardstick: "does ours read like the reference?"
is answerable.

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
- **Tween discipline is `PERFORMANCE.md` §7**: starting a tween is an event; one clocked driver for
  N elements; lifetime-linked; unscaled time through pauses.
- **Retargetable motion wants an integrator, not a tween.** A tween's contract is "fixed target,
  fixed duration" — when the target can change mid-flight (a falling block whose destination drops
  further because blocks beneath it were blasted), a tween must be killed and recreated on every
  retarget (allocation, racing-callback risk, duration re-math). A hand-rolled integrator
  (`velocity += g·dt; position += velocity·dt; settle at current target`) retargets for free: the
  target is just a variable the sim updates. Use tweens for cold, one-shot, fixed-target motion;
  use a clocked driver for hot, retargetable, N-element motion.
- **Hand-rolled ≠ unreadable — the player-class pattern.** The readability of `DOMove(...)` in one
  line comes from its API, not its library, and the same surface is available by hand: each motion
  concern is its **own plain-C# player class** (`FallAnimator`, `BlastFxPlayer` — single
  responsibility, no `MonoBehaviour`) with a declarative one-line entry (`Begin(view, target)`)
  and a `Step(dt)` the owning view calls from a one-line `Update`. Easing lives in named functions
  or `AnimationCurve` fields, so behaviour changes are data changes. This keeps call sites as
  short as a tween call, makes the driver steppable in tests (a tween library's clock is not), and
  contains growing retarget/cancel complexity inside one class instead of a web of
  `Kill`/`OnComplete` callbacks. What this pattern forbids is the naive version: interpolation
  soup inlined into a controller's `Update`.

## 4. Input feel

- **Act on touch-began, not touch-up**, for board taps — the perceived snappiness difference is
  large and the genre standard.
- **Grid hit-testing is math, not physics**: pointer → cell by coordinate arithmetic, no colliders,
  no raycasts (convention, not engine doctrine — the wins are zero physics cost and a strippable
  physics module).
- **Never lock input as an animation side effect.** Invalid taps no-op; valid taps on stationary
  content work while other content animates (the sim/view split makes this free). Input locks are
  explicit, named flow states (a win/fail sequence, a tutorial) with a single owner — and a lock
  is expressed at the input boundary (raycaster off, one gate in the handler), not sprinkled
  through views.
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

## 6. Ship the feel with proof

Polish claims follow the same rule as performance claims (`PERFORMANCE.md` §11): "the tutorial
matches the reference pattern" is demonstrated with a side-by-side capture; "the cascade feels
right" has a device capture at target frame rate. What can't be measured is still *reviewable* —
record it and look.
