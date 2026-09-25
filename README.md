# Slow Burn Romance

A BepInEx plugin for [Ostranauts](https://store.steampowered.com/app/1022980/Ostranauts/). It lets attraction grow between two characters who've become close, even when it falls outside one character's usual orientation. It works one pair at a time and through the game's own relationship numbers.

## How the base game does it

Each character carries attraction flags: `IsAttractedMen`, `IsAttractedWomen`, `IsAttractedNB`, or `IsAttractedNone`. Flirting, inviting for a drink, bawdy talk, charming a fixer and the replies to all of them come in gendered versions, around 77 interactions. Each is gated by condition triggers that require the flag matching the target's gender. If the flag is missing, the reply is "no thanks, not interested." In code, the only hardcoded check is in `Relationship.StoreIAConds`: someone becomes a crush (`RELLover`) once intimacy reaches −50, but only toward a gender they're attracted to.

Each relationship also keeps a per-stat tally of how the other person's dealings have eased or worsened each social need. The game turns that tally into familiarity and a kind or hostile share. Strangers become acquaintances at 50 familiarity. From 200, a kind share over 55% makes a friend and a hostile share over 55% makes an enemy.

## What the mod changes

An NPC may come to feel attraction toward one specific person when all of these hold:

- They're an NPC. The player's orientation stays as chosen at character creation.
- They aren't attracted to no one. Ace and aro characters are never touched, and no setting changes that.
- Their fixed openness falls under `OpenChance`. Openness is a hash of their name, so it survives saves and needs no save data. Raising the chance only adds open characters, and lowering it only removes them.
- The pair involves the player's crew, unless `IncludeNpcPairs` is on.
- Their own relationship with that person has reached `FamiliarityNeeded` familiarity, with at least a `KindnessNeeded` kind share.

For such a pair, a trigger that requires the matching attraction flag is evaluated as a copy without that one requirement, and a trigger that forbids it fails, exactly as if the flag were present. The crush step in `StoreIAConds` gets the same treatment. Nothing is written to anyone's conditions, and the NPC's attraction to everyone else is unchanged.

## Patches

| Target | Kind | Why |
| --- | --- | --- |
| `Interaction.TriggeredInternal` | prefix and finalizer | Records the us/them pair being tested |
| `CondTrigger.Triggered` | prefix | Evaluates attraction triggers for an open pair |
| `Relationship.StoreIAConds` | postfix | The crush step for an open pair |

`Rules.Explain(us, them)` is public. It returns null when the pair is open, or the first reason it isn't. [OstraScope](https://github.com/dataterminals/ostrascope)'s `/social` shows it per pair.

## Config

`BepInEx/config/com.sylvia.slowburnromance.cfg`, created on first launch:

| Setting | Default | |
| --- | --- | --- |
| `General.Enabled` | `true` | Off behaves exactly like the base game |
| `Openness.OpenChance` | `0.35` | Share of characters who can open up |
| `Closeness.FamiliarityNeeded` | `120` | On the game's scale: acquaintance 50, friend or enemy from 200 |
| `Closeness.KindnessNeeded` | `0.6` | Kind share of the relationship tally |
| `Scope.IncludeNpcPairs` | `false` | Let NPCs open up to each other too |

## Build

```bash
dotnet build -c Release -p:Deploy=true -p:GameDir="<game folder>"
```

Requires BepInEx 5 in the game folder. The DLL is deployed to `BepInEx/plugins/SlowBurnRomance/`.
