# Pong Kong (Pong Evolved) — Full Development Plan

> **Engine:** Unity 6 · **Pipeline:** URP · **Networking:** Photon PUN 2  
> **Architecture:** Event-Driven (ScriptableObject EventBus)  
> **Platforms:** Windows, Linux, macOS, Android, iOS  
> **Orientation:** Landscape (Desktop & Mobile)

---

## Table of Contents

1. [Technical Specifications](#technical-specifications)
2. [Architecture Overview](#architecture-overview)
3. [Phase 1 — Basic Setup (Core Classic Pong Loop)](#phase-1--basic-setup-the-core-classic-pong-loop)
4. [Phase 2 — Game Mechanics & Power-Up Economy](#phase-2--game-mechanics--power-up-economy)
5. [Phase 3 — Game Modes, UI & Polish](#phase-3--game-modes-ui--polish)
6. [Milestones Reference](#milestones-reference)
7. [Event Channel Reference](#event-channel-reference)
8. [Scoring & Point Values Reference](#scoring--point-values-reference)
9. [Glossary](#glossary)
10. [Future Roadmap](#future-roadmap)

---

## Technical Specifications

| Property | Value |
|----------|-------|
| Engine | Unity 6 |
| World Space | 2D |
| Render Pipeline | URP (Universal Render Pipeline) |
| Networking | Photon PUN 2 |
| Architecture | Event-Driven — no direct method calls between systems |
| Physics | Unity 2D Physics (Rigidbody2D + custom bounce logic) |
| Orientation | Landscape (Desktop & Mobile) |
| Target Platforms | Windows, Linux, macOS, Android, iOS |

### Game Modes

| Mode | Players | Network | Description |
|------|---------|---------|-------------|
| Single Player | 1 Human + 1 AI | Offline | AI difficulty scales with player performance |
| Local Multiplayer | 2 Humans | Offline | Same device, split keyboard input |
| Online Multiplayer (2P) | 2 Humans | Photon PUN 2 | Ranked or casual |
| Online Multiplayer (4P) | 4 Humans | Photon PUN 2 | Future — top/bottom lanes added |

---

## Architecture Overview

### Why Event-Driven?

All game systems communicate exclusively through a central **EventBus**. No system holds a direct reference to another. This means when the ball hits a ledge, the Ball system doesn't call the PowerUp system directly — it fires an `OnBallHitLedge` event that anyone can listen to. This keeps code decoupled, testable, and easy to extend.

```
[Input System]  ──► [EventBus] ──► [Ledge System]
[Ball System]   ──► [EventBus] ──► [PowerUp System]
[Score System]  ──► [EventBus] ──► [UI System]
[AI System]     ──► [EventBus] ──► [Network System]
```

### Scene Structure

| Scene | Purpose |
|-------|---------|
| `Bootstrap` | App init, service locator, EventBus setup. Loaded first, always. |
| `MainMenu` | Mode selection, settings |
| `GameArena` | Core gameplay — loaded additively on top of Bootstrap |
| `Lobby` | Photon lobby / room browser |
| `Results` | Post-match score screen, MVP badge, stats |

### Folder Structure

```
Assets/
├── _Game/
│   ├── Events/          ← ScriptableObject-based event channels
│   ├── Systems/
│   │   ├── Ball/
│   │   ├── Ledge/
│   │   ├── PowerUp/
│   │   ├── Score/
│   │   ├── AI/
│   │   └── Network/
│   ├── Arenas/
│   ├── UI/
│   ├── Audio/
│   └── VFX/
└── Plugins/
    └── PhotonPUN/
```

---

## Phase 1 — Basic Setup: The Core "Classic Pong" Loop

**Goal:** Don't worry about power-ups, AI, or graphics yet. Get a basic, playable 2D game where two human players can move paddles on the same keyboard and bounce a ball back and forth indefinitely.

---

### Task 1: Project Setup, Folder Structure & EventBus

Create a new Unity 6 project using URP. Set up the folder layout exactly as specified above — a clean structure now prevents a messy pile of files later.

**Create the Bootstrap scene first.** This scene initialises the service locator and the EventBus before any gameplay scene loads. It persists across scene changes.

**Build the EventBus using ScriptableObjects.** Think of the EventBus as a central radio tower. Instead of systems talking directly to each other (messy, hard-to-fix), they broadcast a "radio message" (an Event) that anyone listening can respond to. Use the Ryan Hipple GDC ScriptableObject event pattern as the foundation.

Create the core event channel assets now — even if their listeners don't exist yet. See the [Event Channel Reference](#event-channel-reference) for the full list.

---

### Task 2: Building the Game Arena & Boundaries

Set up the arena boundaries exactly as specified:

```
┌─────────────────────────────────────┐ ← (Solid wall)
│                                     │
│  [P1 Ledge]          [P2 Ledge]    │
│          ●                          │
│                                     │
└─────────────────────────────────────┘ ← (Solid wall)
◄ LEFT WALL  (Hard boundary — P1 miss zone)
RIGHT WALL ► (Hard boundary — P2 miss zone)
```

- **Left and Right walls** are solid hard boundaries. If the ball crosses them, the opposing player scores.
- **Top and Bottom** are solid walls. The ball bounces off them with a clean elastic reflection (preserving horizontal velocity and inverting vertical velocity).

---

### Task 3: Ledge (Paddle) Movement & Input Mapping

Create a simple rectangular Ledge prefab. Map the following inputs:

**Desktop — Keyboard:**

| Player | Up | Down |
|--------|----|------|
| Player 1 | `W` | `S` |
| Player 2 | `↑` | `↓` |

**Desktop — Controller:**

| Player | Control |
|--------|---------|
| Both | Left Stick Y-Axis |

**Mobile:** Each player drags anywhere on their half of the screen vertically to slide their ledge. Touch zones must be a minimum of 44×44 pt for accessibility.

**Ledge base properties:**

| Property | Value |
|----------|-------|
| Default height | 20% of arena height |
| Default width | 1.2 units |
| Movement axis | Vertical only (Y-axis) |
| Movement speed | 8 units/sec (base) |
| Max Y | Arena top bound |
| Min Y | Arena bottom bound |

---

### Task 4: Ball Physics, Bounce Logic & Scoring

**Physics setup:**
- Attach a `PhysicsMaterial2D` to the ball with `bounciness = 1.0` and `friction = 0`. The ball must never lose speed from physics alone.
- Write a custom `BallController` component that overrides Unity's default bounce. The outgoing angle on ledge contact is:

```
outAngle = mirroredIncidentAngle + (hitOffset × deflectionMultiplier)
```

Where `hitOffset` is the normalised distance from ledge center (−1 to 1). Hitting the edge produces a sharp angle; hitting dead center produces a nearly flat return.

**Ball respawn sequence (runs after every point):**
1. Ball fades out at the miss zone.
2. **1.5-second pause** — brief results flash on screen.
3. Ball fades in at center and relaunches.

**Scoring:**
- Ball crosses the **left wall** → Player 2 scores.
- Ball crosses the **right wall** → Player 1 scores.
- Display each player's score prominently in their half of the arena header.
- **Important:** The baseline miss award is **2 points** (not 1), so that spending 1 point on a power-up always has meaningful payoff potential.

**Win condition:**
- Default: first to **20 points** (configurable: 10 / 20 / 30 / custom).
- If tied at the win threshold: **Sudden Death** — next point wins.
- Online disconnect: **10-second reconnect window** offered. If not rejoined, remaining player is declared winner. Match result is recorded even on forfeit.

---

### ✅ Milestone 1: The Physics Loop (End of Phase 1)

**Check criteria:** Boot the game locally, use the keyboard to bounce the ball back and forth indefinitely, watch it bounce off the top and bottom boundaries, and confirm points are awarded when the ball crosses the left or right wall.

---

## Phase 2 — Game Mechanics & Power-Up Economy

**Goal:** Make the game exciting. Add escalating ball speed, breakable ledges, and the full power-up economy.

---

### Task 1: Ball Speed Scaling & Ledge Life System

**Ball speed formula:**

```
currentSpeed = baseSpeed + (totalMatchPoints × speedScalingFactor)
```

| Variable | Default | Notes |
|----------|---------|-------|
| `baseSpeed` | 6 units/sec | Starting speed at score 0–0 |
| `speedScalingFactor` | 0.15 units per point | Tunable per arena |
| `maxSpeed` | 22 units/sec | Hard cap to keep game playable |

Speed **resets to `baseSpeed`** after each point is scored, then re-accumulates. This makes early-game calm and late-game explosive.

**Ledge Life System:**

Each ledge has **3 lives** by default. Lives can only be reduced by specific power-ups (Destroyer Bounce, Ball Vanish). Lives do not regenerate mid-match.

| Lives | Visual State |
|-------|-------------|
| 3 | Full, solid, bright |
| 2 | Small crack on ledge surface |
| 1 | Larger cracks, flickering glow, ledge **shortened by 10%** |
| 0 | Shattered look, red tint, particle debris — **Broken Ledge State** |

**Broken Ledge State:** When a ledge reaches 0 lives, any point scored against that player counts for **double** (2× multiplier applied to all subsequent scores against them for the rest of the match).

---

### Task 2: Power-Up State Machine & HUD

Build the following state machine for all power-ups:

```
[Player presses power-up key]
        │
        ▼
[PRIMED STATE] ──── ball leaves court ────► [DEACTIVATED]
        │
   ball enters court
        │
        ▼
[ACTIVE STATE] ──── effect triggered ────► [DEACTIVATED]
```

- A power-up enters **Primed State** the moment the player buys it — it sits queued but not yet active.
- It becomes **Active** the exact moment the ball enters the activating player's half of the court.
- It deactivates when the effect is consumed, or when the ball leaves the court without triggering.
- **No refund if a primed power-up is not triggered** — this creates strategic risk in pre-activating abilities.

**Power-Up HUD requirements:**
- Numbered quick-slots (keys 1–4 on keyboard; on-screen buttons for mobile).
- Show each power-up's cost alongside its icon.
- Show current **Spendable Points** balance (separate counter from total score).
- Slot is **greyed out** when the player has insufficient points.
- **Active state:** animated glowing border on the slot.
- **Primed state:** pulsing ring on the slot.
- Show total match score (sum of both players) at centre — this communicates ball speed pressure visually.

---

### Task 3: Coding the 4 Core Power-Ups

#### Ball Dash — Cost: 1 Point

**Intent:** Quickly reposition the ledge to guarantee contact. Best paired with another power-up to ensure it isn't wasted.

**Behaviour:** When the ball enters the player's court, the ledge dashes along the Y-axis toward the ball's current Y position.

| Property | Value |
|----------|-------|
| Cost | 1 point |
| Dash speed | 3× normal ledge speed |
| Max dash distance | 35% of arena height |
| Duration | Instant — one-time per activation |

**Visual feedback:** Quick zoom-in pulse on ledge + faint speed-lines / motion blur during the dash + subtle camera zoom-in then release.

---

#### Force Bounce — Cost: 1 Point

**Intent:** Supercharge the ball's outgoing speed after contact, turning a defensive return into an aggressive attack.

**Behaviour:** When the ball contacts the ledge during Active State, outgoing speed is multiplied. Angle of bounce is preserved. The increased speed persists until the next point resets it.

| Property | Value |
|----------|-------|
| Cost | 1 point |
| Speed multiplier | 1.8× current ball speed |
| Duration | Single contact — consumed on use |
| Stacks with | Destroyer Bounce (order matters — apply Force Bounce first) |

**Visual feedback:** Ledge vibrates briefly on contact + ball emits bright impact flash + trail flares + subtle 0.1s screen shake + ledge flashes white/yellow energy sheen.

---

#### Destroyer Bounce — Cost: 2 Points

**Intent:** High-risk, high-reward. Escalates ball speed dramatically within a bounded zone and awards massive points on success.

**Behaviour:** A Destroyer Boundary (glowing energy cage) appears around the activating player's court. Every bounce within the boundary increases ball speed by a fixed increment. The escalated speed and Destroyer state carry over when the ball exits toward the opponent.

| Property | Value |
|----------|-------|
| Cost | 2 points |
| Speed gained per internal bounce | +1.5 units/sec |
| Point multiplier on score | 3× |
| Miss penalty (opponent gains) | 4 points (8 if ledge is broken) |
| Ledge life damage on hit | −1 life |
| Duration | Until ball exits player's court |
| Stacks with | Force Bounce (apply Force Bounce first for maximum speed) |
| Disallowed combo | Ball Vanish (too oppressive — Destroyer cancels Vanish) |

**Visual feedback:** Glowing crackling energy cage appears + escalating screen shake on each internal bounce + red chromatic aberration pulse + ball trail turns deep red/orange + ball grows slightly with each bounce + opponent's HUD flashes a warning indicator.

---

#### Ball Vanish — Cost: 4 Points

**Intent:** Disorienting, high-risk ability that makes the ball temporarily invisible, forcing the opponent into a psychological guessing game.

**Behaviour:** Ball enters a Visible → Invisible → Visible cycle. While invisible, the ball continues to physically exist and bounce off top/bottom walls but does not trigger scoring on wall contacts.

| Phase | Duration |
|-------|----------|
| Visible | 1.2 seconds |
| Invisible | 0.8 seconds |
| Full cycle | 2.0 seconds |

When the ball becomes Visible again, it "snaps" to its real position — potentially appearing somewhere unexpected.

| Property | Value |
|----------|-------|
| Cost | 4 points |
| Miss penalty (misser loses) | 1 point |
| Score on opponent miss | 3× normal (8 points base) |
| Broken Ledge miss score | 16 points |
| Duration | Until ball exits player's court |
| Stacks with | Ball Dash (Dash ensures you can return during visible phase) |
| Disallowed combo | Destroyer Bounce |

**Visual feedback:** Ball flickers like a hologram before vanishing + during invisible state, a faint ghost outline is visible **only to the ball owner** (completely invisible to the opponent) + screen gains subtle vignette and desaturation effect + mysterious ambient sound loop + on reappear: brief radial flash from ball's position.

---

### Power-Up Stacking Reference

| Power-Up | Stacks With | Notes |
|----------|-------------|-------|
| Ball Dash | Any | Use to ensure another power-up connects |
| Force Bounce | Ball Dash, Destroyer Bounce | Apply Force Bounce before Destroyer for max speed |
| Destroyer Bounce | Force Bounce | Disallowed with Ball Vanish |
| Ball Vanish | Ball Dash | Disallowed with Destroyer Bounce |

---

### ✅ Milestone 2: Economy Active (Mid-Phase 2)

**Check criteria:** Scoring a goal awards spendable points. Points update reliably on the HUD. The ball visibly speeds up as the combined match score climbs. The Primed/Active state machine fires correctly.

### ✅ Milestone 3: Arsenal Ready (End of Phase 2)

**Check criteria:** All 4 power-ups can be bought, enter their queued Primed slots correctly, activate on the ball's court entry, and execute their effects without breaking the game. Stacking rules and disallowed combos are enforced. Broken Ledge visual states render correctly at each life count.

---

## Phase 3 — Game Modes, UI & Polish

**Goal:** Turn the prototype into a polished, complete product. Add an AI opponent, real-time online multiplayer, mobile support, and full visual/audio juice.

---

### Task 1: Smart AI Opponent

Program the AI using a **three-layer decision stack:**

**Layer 1 — Ball Tracking (always active):**
- AI predicts the ball's Y position at time of arrival using linear trajectory simulation plus top/bottom wall bounce simulation.
- Moves the ledge toward the predicted intercept point.
- Intentional error (random offset from true position) is introduced based on difficulty level.

**Layer 2 — Power-Up Logic (active when points ≥ cost):**
- AI evaluates whether to spend points based on: current score deficit/lead, current ball speed, and own ledge life remaining.
- Damaged ledge → more defensive, increased Ball Dash usage.
- Hard difficulty: can chain Ball Dash + Force Bounce optimally.

**Layer 3 — Adaptive Difficulty:**
- Tracks the player's win rate over the **last 5 points**.
- Player winning → AI tightens reaction time and increases power-up frequency.
- Player losing → AI introduces artificial hesitation and power-up delays.
- Goal: maintain approximately **50% win rate** for the player to keep the match tense.

**AI difficulty levels:**

| Difficulty | Reaction Time | Prediction Accuracy | Power-Up Use | Error Rate |
|------------|--------------|-------------------|--------------|------------|
| Easy | 400ms | 60% | Rare, random | High |
| Medium | 200ms | 80% | Occasional, semi-smart | Medium |
| Hard | 80ms | 95% | Frequent, strategic | Low |
| Adaptive | Scales | Scales | Scales | Matches player win rate |

**AI listens to these events:**
- `OnBallPositionUpdated` → recalculate intercept
- `OnBallEntersCourt(AI)` → evaluate power-up activation
- `OnPointScored` → update adaptive difficulty model

---

### Task 2: Photon PUN 2 Online Multiplayer

#### Room Structure

| Property | Value |
|----------|-------|
| Max players per room | 2 (4 in future 4P mode) |
| Room visibility | Public (Quick Match) / Private (code-based) |
| Region | Auto-selected by ping |
| Matchmaking | ELO-based in Ranked; open in Casual |

#### Network Authority Model

| System | Authority |
|--------|-----------|
| Ball position & physics | Host (Master Client) — simulated on host, synced to client |
| Ledge position | Local owner — each player sends their own position |
| Power-up activation | Local owner sends event → host validates and broadcasts |
| Score | Host — authoritative, cannot be spoofed |
| AI (single-player) | Local — no network involvement |

#### Photon RPCs

| RPC Name | Sender | Purpose |
|----------|--------|---------|
| `RPC_BallState` | Host | Sync ball position + velocity every fixed tick |
| `RPC_PowerUpActivated` | Any | Notify both clients of power-up trigger |
| `RPC_ScoreUpdate` | Host | Broadcast updated scores |
| `RPC_LedgeLifeUpdate` | Host | Broadcast ledge life change |
| `RPC_MatchEnd` | Host | Trigger end-of-match on both clients |

#### Lag Compensation

- Ledge movement uses **client-side prediction + server reconciliation**.
- Ball position uses **interpolation on the client with a 1-frame buffer**.
- On detected desync (>50ms position difference): host state is authoritative and client snaps.
- Visual rubber-banding is smoothed over **0.2 seconds** to avoid jarring corrections.

#### Disconnect Handling

- On disconnect mid-match: a **10-second reconnect window** is offered.
- If player does not rejoin: remaining player is declared winner.
- Match result is recorded even on disconnect forfeit.

---

### Task 3: UI Layouts & Mobile Adaptation

**Main Menu:**
- Animated arena loop in the background (ball bouncing, ledges idling).
- Menu items: Play, Settings, Leaderboard (future), Skins (future), Quit.
- Mode selection uses large tap-friendly cards with mode descriptions.
- Mode flow:
  ```
  Main Menu → Play
  ├── Single Player → Difficulty Select → Arena Select → Game
  ├── Local Multiplayer → Arena Select → Game
  └── Online
      ├── Quick Match → Matchmaking → Lobby → Game
      └── Private Room → Create/Join with code → Lobby → Game
  ```

**In-Game HUD:**
```
┌──────────────────────────────────────────────────┐
│ [P1 Score: 12]     [Total: 24]    [P2 Score: 12] │
│ [Spendable: 6]                    [Spendable: 4] │
│                                                  │
│ [🟡1][⚡1][💥2][👁4]       [🟡1][⚡1][💥2][👁4] │
│                                                  │
│ [P1 Lives: ❤❤❤]            [P2 Lives: ❤❤○]     │
└──────────────────────────────────────────────────┘
```

**Online Lobby:**
- Room code display (Private) / Auto-matching spinner (Quick Match).
- Ready toggle button.
- Arena vote (future) and chat (future).

**Results Screen:**
- Final scores displayed with animation.
- **Power-up usage stats:** times used per power-up, points spent vs earned per power-up.
- **MVP badge:** awarded to the player with the highest single power-up contribution.
- Actions: Play Again / Return to Menu / Share (future).

**Pause Menu (Single-Player / Local):**
- Resume / Restart / Settings / Quit to Menu.

**Mobile touch input:**
- Each player drags anywhere on their **half of the screen** vertically to slide their ledge.
- Touch zones: minimum **44×44pt** on all interactive elements.

**Accessibility:**
- All colour-coded elements must have distinct **shapes** as a backup cue.
- Font size scales with device resolution.
- Haptic feedback and audio can be **independently toggled** in settings.

---

### Task 4: Visual & Audio Juice

#### Ball Visual States

| State | Appearance |
|-------|------------|
| Normal | White circle with soft glow trail |
| Force Bounce primed | Yellow/orange outline pulsing |
| Destroyer State | Red core, crackling particle trail, grows per bounce |
| Ball Vanish (visible phase) | Shifting translucent blue, edge flicker |
| Ball Vanish (invisible phase) | Invisible to opponent; ghost ring to owner |
| High speed (near cap) | Heavily elongated, long trail, colour shifts to hot white |

#### Ball Deformation (Squash-and-Stretch Shader)

| Event | Deformation |
|-------|-------------|
| High-speed travel | Elongated in direction of travel |
| Ledge contact | Brief squash perpendicular to travel direction |
| Idle / slow speed | Near-perfect circle |
| Force Bounce activation | Sharp elongation spike on contact |
| Destroyer Bounce impact | Brief inflation + shockwave ripple |

The deformation magnitude is capped to avoid visual distortion at extreme speeds.

**Ball trail:** Soft-glow particle trail follows the ball. Trail length and opacity scale with ball speed. Trail colour changes per active power-up state.

#### Screen Feedback Per Event

| Event | Screen Effect |
|-------|--------------|
| Destroyer Bounce (each internal bounce) | Escalating camera shake + red chromatic aberration pulse |
| Ball Vanish activation | Vignette darkens + mild desaturation |
| Ball Dash | Brief zoom-in (1.05×) then release |
| Force Bounce contact | Short 0.08s vibration shake |
| Ball miss | Quick flash of the scoring player's colour |
| Ledge life lost | Screen crack overlay fades in over 0.3s |
| Ledge broken (0 lives) | Heavy shake + red flash + crack texture persists |
| Point scored | Score counter animates + brief 0.2s slowdown (0.4× timescale) |

#### Audio Design

| Sound | Trigger | Notes |
|-------|---------|-------|
| Ball–Ledge hit | Each contact | Crisp, pitched slightly higher at higher speeds |
| Ball–Wall miss | Miss event | Deep impact + score increment sound |
| Ball bounce | Top/bottom | Soft click / bounce sound |
| Ball Dash | Activation | Snap/thump with reverb tail |
| Force Bounce | On contact | Electric crack, pitched high |
| Destroyer (each bounce) | Internal bounce | Low-frequency thud, escalating pitch |
| Ball Vanish activate | Activation | Ethereal reverse-whoosh |
| Ball Vanish flicker | Each phase toggle | Glitch/static click |
| Ledge life lost | Damage event | Crunch + glass crack |
| Ledge broken | 0 lives | Heavy smash SFX + deep rumble |
| Point scored | Score event | Fanfare stab (custom per arena) |
| Match win | Match end | Victory melody |
| Match loss | Match end | Deflated descending tone |

#### Haptic Feedback (Mobile)

| Event | Pattern |
|-------|---------|
| Ball–Ledge contact | Light tap |
| Force Bounce | Medium pulse |
| Destroyer internal bounce | Heavy rumble (escalating) |
| Ball Vanish toggle | Double-tap (light) |
| Point scored | Medium pulse + pause |
| Ledge broken | Strong sustained buzz |

#### Art Direction

- Style: **Neon-on-dark minimalism.** Clean geometric shapes with vibrant energy effects. Inspired by classic arcade aesthetics updated with modern shader work.
- Colour palette: Deep navy/black backgrounds; cyan, magenta, white for primary elements; power-up-specific accent colours.
- Resolution: 1920×1080 desktop target; adaptive for mobile.

---

### ✅ Milestone 4: Human-Like AI (Mid-Phase 3)

**Check criteria:** Play the game fully offline against the computer. The AI uses power-up combos intelligently, adjusts its skill level on the fly to match your performance, and does not feel robotic or predictable.

### ✅ Milestone 5: Netplay Certified (Mid-Phase 3)

**Check criteria:** Two separate devices connect over the internet into a private match lobby, launch the game, and experience smooth gameplay with zero desync or rubber-banding. Disconnect handling works correctly.

### ✅ Milestone 6: Feature Complete / Version 1.0 (End of Phase 3)

**Check criteria:** Smooth mobile touch controls, dynamic audio and haptic feedback, squash-and-stretch ball physics, all menu structures functional, win condition and Sudden Death work, Results screen shows MVP badge and power-up stats. Game is ready to build and publish.

---

## Milestones Reference

| # | Milestone | Phase | Check Criteria |
|---|-----------|-------|----------------|
| 1 | Physics Loop | End of P1 | Ball bounces indefinitely, bounces off top/bottom walls correctly, points awarded on miss |
| 2 | Economy Active | Mid-P2 | Points awarded correctly, HUD updates, ball visibly speeds up |
| 3 | Arsenal Ready | End of P2 | All 4 power-ups functional, stacking rules enforced, ledge states correct |
| 4 | Human-Like AI | Mid-P3 | Offline AI match playable; adaptive difficulty works |
| 5 | Netplay Certified | Mid-P3 | Online 2P match stable with no desync or rubber-banding |
| 6 | Feature Complete | End of P3 | All systems integrated, mobile-ready, publishable build |

---

## Event Channel Reference

All events are ScriptableObject-based event channels. Create these as assets in `Assets/_Game/Events/`.

| Event Channel | Payload | Listeners |
|---------------|---------|-----------|
| `OnBallLaunched` | `Vector2 direction` | BallVFX, AudioSystem |
| `OnBallPositionUpdated` | `Vector2 position` | AISystem, NetworkSync |
| `OnBallEnteredCourt` | `PlayerSide side` | PowerUpSystem, AISystem |
| `OnBallExitedCourt` | `PlayerSide side` | PowerUpSystem |
| `OnBallHitLedge` | `ContactData` | PowerUpSystem, VFXSystem, AudioSystem |
| `OnBallHitWall` | `WallSide side` | ScoreSystem, AudioSystem |
| `OnPointScored` | `PointScoredData` | ScoreSystem, UISystem, BallSystem |
| `OnPowerUpPrimed` | `PowerUpType, PlayerSide` | PowerUpSystem, UISystem |
| `OnPowerUpActivated` | `PowerUpType, PlayerSide` | PowerUpSystem, VFXSystem, AudioSystem |
| `OnPowerUpDeactivated` | `PowerUpType, PlayerSide` | PowerUpSystem, UISystem |
| `OnLedgeLifeChanged` | `PlayerSide, int lives` | LedgeSystem, UISystem, VFXSystem |
| `OnMatchEnd` | `WinnerSide` | UISystem, NetworkSystem |
| `OnAIDecision` | `AIAction` | LedgeSystem (AI-controlled) |

### Event Flow Example — Force Bounce

```
Player presses Force Bounce key
        │
        ▼
PowerUpSystem.OnForceBouncePrimed
        │ fires: OnPowerUpPrimed(ForceBounce, P1)
        ▼
UISystem → highlight Force Bounce slot

Ball enters P1 court
        │ fires: OnBallEnteredCourt(P1)
        ▼
PowerUpSystem → activates Force Bounce for P1
        │ fires: OnPowerUpActivated(ForceBounce, P1)
        ▼
VFXSystem → ledge glow
AudioSystem → readiness sound

Ball hits P1 ledge
        │ fires: OnBallHitLedge(contactData)
        ▼
PowerUpSystem → detects active ForceBounce
    → applies 1.8× speed multiplier to ball
        │ fires: OnPowerUpDeactivated(ForceBounce, P1)
        ▼
VFXSystem → impact flash + ball trail flare
AudioSystem → electric crack SFX
UISystem → dim Force Bounce slot
```

---

## Scoring & Point Values Reference

| Event | Points Awarded To |
|-------|------------------|
| Normal miss | Opponent: **2 points** |
| Destroyer Bounce miss | Opponent: **4 points** (3× score = 6 base, but miss baseline = 4) |
| Ball Vanish miss | Opponent: **8 points** + Misser loses 1 point |
| Broken Ledge — normal miss | Opponent: **4 points** (2× modifier) |
| Broken Ledge + Destroyer | Opponent: **8 points** |
| Broken Ledge + Vanish | Opponent: **16 points** |

### Power-Up Costs

| Power-Up | Cost |
|----------|------|
| Ball Dash | 1 point |
| Force Bounce | 1 point |
| Destroyer Bounce | 2 points |
| Ball Vanish | 4 points |

---

## Glossary

| Term | Definition |
|------|------------|
| Ledge | The player-controlled paddle used to deflect the ball |
| Court | The half of the arena belonging to a player |
| Primed State | A power-up is selected and queued, waiting for the ball to enter the player's court |
| Active State | A power-up is live and its effect can trigger |
| Top/Bottom Wall | Top and bottom boundaries — the ball bounces off these elastically |
| Miss Zone | Left and right boundaries — ball passing these scores a point |
| Destroyer State | Ball state during Destroyer Bounce, carrying escalating speed and point multipliers |
| Broken Ledge | A ledge with 0 lives remaining, granting 2× points to the opponent on all misses |
| Total Match Score | Combined score of both players; used to calculate ball speed scaling |
| Spendable Points | A player's current point balance available to spend on power-ups |
| Host | The Photon Master Client; authoritative for ball simulation and scoring |
| EventBus | The central ScriptableObject event channel system used for all inter-system communication |

---

## Future Roadmap

### Phase 1 — Polish & Core Completion
- Smooth ledge inertia and in-place stopping
- Full VFX pass on all power-ups
- Mobile touch input refinement
- Online leaderboards (Photon or custom backend)

### Phase 2 — Content Expansion
- **4-Player Multiplayer:** Top and bottom ledges added; Team 2v2 or free-for-all
- **Additional power-ups:**
  - Gravity Shift — pulls or pushes the ball vertically temporarily
  - Decoy Ball — spawns a fake ball for 2 seconds
  - Ledge Heal — restore 1 ledge life
  - Speed Freeze — momentarily caps ball speed at current value
- **Power-up upgrades:** spend in-game currency to upgrade power-up stats
- **Arena Expansion:** new arenas, each with a unique physics twist

### Phase 3 — Monetisation & Meta
- In-app currency / points (earned through play; used for cosmetics)
- Cosmetics: ball skins, ledge skins, arena skins, hit effect skins
- Season leaderboards (global and friends-only)
- Dynamic animated power-up HUD icons

### Phase 4 — Arena Physics System
- Each arena introduces a named physics rule (e.g. "Gravity Well" pulls the ball toward centre)
- Arena introduction cinematic / flythrough on first play
- Per-arena configurable properties:

| Property | Configurable |
|----------|-------------|
| Background skin | Yes |
| Speed scaling factor | Yes |
| Top/bottom wall visuals | Yes |
| Environmental physics modifiers | Yes (Phase 4) |
| Music track | Yes |
| Power-up cost multiplier | Yes (Phase 4) |

---

*Document maintained by the development team. Version history tracked in source control.*
