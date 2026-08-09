# Review Checklist

The enforcement surface for this set. The other documents explain *why*; this one is the moment the
rules get **asked**, which is the part that was missing when rules in this set were violated by the
projects that had written them down.

**It asks questions; it never restates rules.** Every line points at the section that owns the
answer, so this file cannot drift out of agreement with the set — it has no content of its own to
contradict. If a question and its section disagree, the section wins and the question is wrong.

**Each section stays one screen.** The sections run at different moments — per piece, at a gate,
when editing the set — so no sitting ever faces the whole file; but a section that has to be
scrolled gets skimmed, and a skimmed checklist is worse than none because it produces the feeling
of having checked. Items earn their place by having actually gone wrong; a rule that has never been
broken does not need a line here.

**Projects extend it, they don't fork it.** A project keeps its own checklist for its own rules
(house style, its test command, its domain vocabulary) and treats this one as the portable base.

---

## Before presenting a piece of work

- [ ] Right layer, right assembly — no framework or `async` in the pure core? (`ARCH` §1, §5)
- [ ] Does anything run after startup that the composition root is wiring itself? (`ARCH` §6)
- [ ] Every `+=` has a written `-=`, or the binding is a deliberate assignable delegate with the
      choice recorded where the callback is declared? (`ARCH` §9, `UNITY` §5)
- [ ] Do algorithms ask what a thing *does* rather than what it *is*? (`CONV` §3)
- [ ] Expected failure returns `Result`; a broken invariant throws; a missing answer throws and a
      neutral answer returns? (`CONV` §4)
- [ ] Does any multi-step operation with more than one call path have a **single owner** for its
      ordering, rather than a comment describing it? (`ARCH` §8a)
- [ ] Does the core boundary take a command and return an outcome — with any presentation-derived
      fact carried *in* the command, not fetched by a callback? (`ARCH` §8)
- [ ] Does every new runtime hook name what forces it to be clocked, or does an existing flow point
      already give the guarantee? (`PERF` §5)
- [ ] Zero allocation on the steady-state path — and is that path **named**, with the deliberate
      exceptions stated? (`PERF` §4)
- [ ] If it loads through Addressables synchronously, is every guardrail satisfied?
      (`ARCH` §5a)
- [ ] New tests for happy path, both boundaries, buffer reuse, determinism — and if something was
      *removed* by decision, a test pinning its absence? (`CONV` §5)
- [ ] Did the same piece of work update the documents that describe the behaviour it changed?
      (`CONV` §5)
- [ ] Asset diffs read before committing — no reimport-reset sizes, no edit-mode layout baked into
      prefabs, one writer for each `ScriptableObject`? (`UNITY` §8)

## At a gate, before shipping or claiming

- [ ] Is every performance claim attached to a capture, **attributed by owner**, and scoped to the
      path it covers? (`PERF` §11)
- [ ] Was anything verified *only* in the editor that belongs to a build- or device-only failure
      class — stripping, Addressables, texture formats, engine-clocked lifetimes? (`UNITY` §9)
- [ ] Have the measuring instruments come out of the shipped build, leaving the evidence?
      (`PERF` §11)
- [ ] Pools sized from a sweep of shipped content, not from a guess? (`PERF` §4, §4a)
- [ ] Does the content still pass its own difficulty gate? (`GAME-FEEL` §6)
- [ ] Were the effects and feel values **seen in play**, not only reasoned about — and are the
      deliberate deviations from the reference recorded next to their numbers?
      (`GAME-FEEL` §1, §5)
- [ ] Is the content schema's strictness a stated posture for the delivery model actually in use?
      (`ARCH` §11)

## When editing this set

- [ ] Does every quotation carry the page it came from, and is every unsourced claim marked as a
      working model with what would settle it? (`README`, "How these docs are written")
- [ ] Does any claim outrun its source — "documented" for something merely recommended, "always"
      for something conditional, a number without its platform? (`README`)
- [ ] If a practice is endorsed with conditions, are the conditions a visible list rather than
      prose inside the endorsement? (`ARCH` §5a is the worked example)
- [ ] When prose was promoted to a list, was it **restated** rather than copied — with the
      precision raised to match the prominence? (`README`)
- [ ] Does the canonical copy live in exactly one place, with every other mention pointing at it
      rather than paraphrasing it? (`README`)
