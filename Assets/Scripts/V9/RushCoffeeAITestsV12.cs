using System;
using System.Collections.Generic;
using UnityEngine;

namespace EarlyRush.V9
{
    /// <summary>Default-population checks and clearly arranged fixtures driven by real Tick/input.</summary>
    public static class RushCoffeeAITestsV12
    {
        static int assertions;
        static void Check(bool value, string message)
        {
            assertions++;
            if (!value) throw new InvalidOperationException("V12 coffee AI test failed: " + message);
        }

        public static int RunAll()
        {
            assertions = 0;
            PopulationAndPublicDesks();
            DashGuardSpillAndReturn();
            PauseFreezesCoffeeGuard();
            DeskDwellAndNewTrip();
            DashAwayDoesNotGuard();
            Debug.Log("V12_COFFEE_ASSERTIONS count=" + assertions);
            return assertions;
        }

        static void PopulationAndPublicDesks()
        {
            var run = new RunV9(2, 91026);
            int cups = 0;
            var previous = new Dictionary<int, Vector2>();
            foreach (var person in run.people)
            {
                if (person.kind != PersonKind.Coffee) continue;
                cups++;
                previous[person.id] = person.position;
                Check(person.coffeeAIInitialized && !person.canRush && !person.spilled, "generated full cup has cautious independent AI");
                Check(person.walkingSpeed >= .9f && person.walkingSpeed <= 1.05f && run.CoffeePersonalSpace(person) > .2f,
                    "carrying is slower and has extra steering space");
                Check(run.map.IsWalkable(person.coffeeDesk, .5f) && !run.IsPedestrianReserved(person.coffeeDesk, .7f),
                    "personal stopping point is usable public ground outside task workspaces");
                bool publicRoom = false;
                foreach (var zone in run.map.zones)
                    if (zone.kind == "office-workroom" && zone.bounds.Contains(person.coffeeDesk)) publicRoom = true;
                Check(publicRoom, "personal stopping point belongs to a public office workroom");
                foreach (var fixture in run.fixtures)
                    if (fixture.kind == FixtureKind.FileDesk)
                        Check(Vector2.Distance(person.coffeeDesk, fixture.position) > 2.7f, "personal desk is not a delivery station");
            }
            Check(run.people.Count == run.config.pedestrianOfficeCount && cups == Mathf.Min(40, (run.config.pedestrianOfficeCount + 2) / 7),
                "configured population and the existing coffee coworker proportion are retained");
            run.Begin();
            run.Tick(2, Vector2.zero);
            int moved = 0;
            foreach (var person in run.people)
                if (person.kind == PersonKind.Coffee && Vector2.Distance(person.position, previous[person.id]) > .2f) moved++;
            Check(moved > 0, "generated coffee coworkers physically walk while the player waits");
            Check(run.tasks[0].count == 0, "coffee locomotion does not write the spill task ledger");
        }

        static void ClearCorridor(RunV9 run, out Vector2 start, out Vector2 direction)
        {
            for (float distance = 18; distance < run.map.PathLength - 18; distance += 2)
            {
                Vector2 point = run.map.PointAtDistance(distance);
                Vector2 forward = (run.map.PointAtDistance(distance + 1) - point).normalized;
                bool usable = forward.sqrMagnitude > .9f;
                for (float step = -4; step <= 10; step += .5f)
                {
                    Vector2 sample = point + forward * step;
                    if (!run.map.IsWalkable(sample, .9f) || run.IsPedestrianReserved(sample, .9f)) usable = false;
                }
                if (usable) { start = point; direction = forward; return; }
            }
            throw new InvalidOperationException("No public coffee fixture corridor");
        }

        /// <summary>Controlled initial placement; never writes spilled or a final CoffeeState.</summary>
        public static RunV9 CreateDashFixture(out PersonV9 person, out Vector2 dashDirection)
        {
            var run = new RunV9(2, 91026, new ConfigV9
            { pedestrianStreetCount = 0, pedestrianMetroCount = 0, pedestrianOfficeCount = 0 });
            ClearCorridor(run, out Vector2 start, out Vector2 forward);
            person = new PersonV9
            {
                id = 8900, kind = PersonKind.Coffee, position = start, home = start, radius = .42f,
                hasPedestrianAI = true, profile = PedestrianProfile.Cautious, walkingSpeed = 1.1f,
                pedestrianRandom = 91027u, art = "people/woman"
            };
            if (!run.AssignPedestrianDestination(person, start + forward * 8)) throw new InvalidOperationException("Coffee fixture has no initial trip");
            run.InitializeCoffeeAI(person);
            run.people.Add(person);
            run.player = start + forward * 2.7f;
            dashDirection = -forward;
            return run;
        }

        static void DashGuardSpillAndReturn()
        {
            var run = CreateDashFixture(out var person, out var direction);
            float radius = person.radius;
            int before = run.tasks[0].count;
            run.Begin();
            Check(run.TryDash(direction), "fixture starts a real player dash");
            bool guard = false, hitWhileGuarding = false;
            for (int frame = 0; frame < 30 && !person.spilled; frame++)
            {
                bool guardingBeforeContact = person.coffeeState == CoffeeState.GuardingCup;
                run.Tick(1f / 60, direction);
                guard |= person.coffeeState == CoffeeState.GuardingCup;
                hitWhileGuarding |= guardingBeforeContact && person.spilled;
            }
            Check(guard && person.coffeeGuardCount > 0, "approaching dash naturally causes a guard response");
            Check(hitWhileGuarding, "the actual dash collision can spill the cup during the guard state");
            Check(person.spilled && run.tasks[0].count == before + 1, "real contact still spills a guarded cup exactly once");
            Check(person.radius == radius && person.coffeeState == CoffeeState.SpillReaction, "guard has no collision immunity and spill triggers a reaction");
            run.Tick(.25f, Vector2.zero);
            Check(person.coffeeState == CoffeeState.SpillReaction && person.velocity == Vector2.zero, "spill reaction stops voluntary walking briefly");
            run.Tick(1, -direction);
            for (int frame = 0; frame < 180; frame++) run.Tick(1f / 60, (person.position - run.player).normalized);
            Check(person.spilled && run.tasks[0].count == before + 1, "repeat physical contact cannot refill or charge the same cup again");
            int contacts = 0;
            foreach (var entry in run.telemetry) if (entry.type == "contact" && entry.id == person.id) contacts++;
            Check(contacts >= 2, "repeat-ledger check includes a second separated physical contact");
            run.Tick(4, -direction);
            for (int frame = 0; frame < 120 * 60 && person.coffeeState != CoffeeState.EmptyAtDesk && run.phase == RunPhase.Playing; frame++)
                run.Tick(1f / 60, Vector2.zero);
            Check(person.coffeeState == CoffeeState.EmptyAtDesk && Vector2.Distance(person.position, person.coffeeDesk) < .8f,
                "empty cup returns to the personal place by physical navigation");
            run.Tick(3, Vector2.zero);
            Check(person.spilled && run.tasks[0].count == before + 1 && person.coffeeState == CoffeeState.EmptyAtDesk,
                "resting at the personal place never resets the cup ledger");
            PushEmptyCoworkerAndObserveReturn(run, person, before + 1);
        }

        static void PushEmptyCoworkerAndObserveReturn(RunV9 run, PersonV9 person, int spillCount)
        {
            Vector2 push = Vector2.zero, behind = Vector2.zero;
            Vector2 restingPosition = person.position;
            foreach (var direction in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
            {
                Vector2 from = restingPosition - direction * 1.35f;
                if (!run.PedestrianLineClear(from, restingPosition + direction * 5, .48f)) continue;
                push = direction; behind = from; break;
            }
            Check(push != Vector2.zero, "returned coworker has room for a controlled physical push");
            var approach = run.FindPedestrianPath(run.player, behind, person.position);
            Check(approach != null, "player can walk around the resting coworker to the push start");
            run.SetBracing(true);
            for (int point = 1; point < approach.Count; point++)
            {
                for (int frame = 0; frame < 30 * 60 && Vector2.Distance(run.player, approach[point]) > .18f; frame++)
                    run.Tick(1f / 60, Vector2.ClampMagnitude((approach[point] - run.player) * 2, 1));
                Check(Vector2.Distance(run.player, approach[point]) <= .18f, "player reaches push approach through real movement");
            }
            run.Tick(.2f, Vector2.zero);
            run.SetBracing(false);
            bool returning = false;
            float displaced = 0, largestStep = 0;
            for (int frame = 0; frame < 90; frame++)
            {
                Vector2 previous = person.position;
                run.Tick(1f / 60, (person.position - run.player).normalized);
                displaced = Mathf.Max(displaced, Vector2.Distance(person.position, person.coffeeDesk));
                largestStep = Mathf.Max(largestStep, Vector2.Distance(previous, person.position));
                returning |= person.coffeeState == CoffeeState.ReturningEmpty;
            }
            Check(displaced > .9f && returning && largestStep < .2f,
                "real player contact pushes the empty coworker away and triggers walking return without a warp");
            run.Tick(1.5f, (run.player - person.position).normalized);
            for (int frame = 0; frame < 30 * 60 && person.coffeeState != CoffeeState.EmptyAtDesk; frame++)
                run.Tick(1f / 60, Vector2.zero);
            Check(person.coffeeState == CoffeeState.EmptyAtDesk && Vector2.Distance(person.position, person.coffeeDesk) < .8f,
                "displaced empty coworker physically reaches the same personal place again");
            Check(person.spilled && run.tasks[0].count == spillCount, "push and second return preserve the unique spill ledger");
        }

        static void PauseFreezesCoffeeGuard()
        {
            var run = CreateDashFixture(out var person, out var direction);
            run.Begin();
            Check(run.TryDash(direction), "pause fixture starts a real dash");
            run.Tick(2f / 60, direction);
            Check(person.coffeeState == CoffeeState.GuardingCup, "pause fixture reaches guard through actual input");
            run.Pause();
            Vector2 position = person.position;
            float timer = person.coffeeStateLeft;
            double elapsed = run.elapsed;
            run.Tick(2, direction);
            Check(person.position == position && person.coffeeStateLeft == timer && run.elapsed == elapsed,
                "pause freezes coffee reaction and movement clocks");
        }

        static void DeskDwellAndNewTrip()
        {
            var run = CreateDashFixture(out var person, out _);
            Vector2 desk = person.coffeeDesk;
            bool positioned = false;
            foreach (var offset in new[] { Vector2.up, Vector2.right, Vector2.down, Vector2.left })
            {
                Vector2 start = desk + offset * 2;
                if (!run.PedestrianLineClear(start, desk, .5f)) continue;
                person.position = start;
                if (run.AssignPedestrianDestination(person, desk)) { positioned = true; break; }
            }
            Check(positioned, "desk fixture has an ordinary short approach");
            run.player = run.map.spawn;
            run.Begin();
            for (int frame = 0; frame < 15 * 60 && person.coffeeState != CoffeeState.DeskPause; frame++) run.Tick(1f / 60, Vector2.zero);
            Check(person.coffeeState == CoffeeState.DeskPause && person.coffeeDeskVisits == 1, "real desk arrival starts a short cup pause");
            Vector2 stopped = person.position;
            run.Tick(.3f, Vector2.zero);
            Check(person.position == stopped && !person.spilled, "cup pause holds the public room edge without spilling");
            run.Tick(4, Vector2.zero);
            Check(person.coffeeState == CoffeeState.Carrying && Vector2.Distance(person.destination, desk) >= 10 && person.destinationChanges > 1,
                "desk dwell leads to an independently selected public errand");
        }

        static void DashAwayDoesNotGuard()
        {
            var run = CreateDashFixture(out var person, out var toward);
            run.Begin();
            Check(run.TryDash(-toward), "away-dash fixture starts normally");
            run.Tick(.2f, -toward);
            Check(person.coffeeGuardCount == 0 && !person.spilled, "a dash moving away does not trigger a false guard or spill");
        }
    }
}
