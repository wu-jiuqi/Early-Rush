using System.Collections.Generic;
using UnityEngine;

namespace EarlyRush.V9
{
    public enum CoffeeState { Carrying, GuardingCup, DeskPause, SpillReaction, ReturningEmpty, EmptyAtDesk }

    public sealed partial class PersonV9
    {
        public bool coffeeAIInitialized;
        public CoffeeState coffeeState;
        public Vector2 coffeeDesk;
        public int coffeeGuardCount, coffeeDeskVisits;
        internal float coffeeStateLeft, coffeeGuardCooldown, coffeeCarrySpeed;
        internal bool coffeeGoingToDesk;
    }

    public sealed partial class RunV9
    {
        readonly List<Vector2> coffeeDeskCandidates = new List<Vector2>();
        bool coffeeDeskCandidatesReady;

        /// <summary>Called after the initial generic trip. Never changes the spilled ledger.</summary>
        internal void InitializeCoffeeAI(PersonV9 person)
        {
            if (person.kind != PersonKind.Coffee || !person.hasPedestrianAI || person.coffeeAIInitialized) return;
            person.coffeeAIInitialized = true;
            person.canRush = false;
            person.profile = PedestrianProfile.Cautious;
            person.coffeeCarrySpeed = .92f + NextPedestrianRandom(person) * .12f;
            person.walkingSpeed = person.coffeeCarrySpeed;
            person.coffeeState = CoffeeState.Carrying;
            person.coffeeGoingToDesk = true;
            person.coffeeDesk = ChooseCoffeeDesk(person);
            // Keep the already valid generic route if an exceptional fixture has no desk route.
            if (!AssignPedestrianDestination(person, person.coffeeDesk))
                person.coffeeDesk = person.destination;
        }

        void BuildCoffeeDeskCandidates()
        {
            if (coffeeDeskCandidatesReady) return;
            coffeeDeskCandidatesReady = true;
            foreach (Vector2 point in PedestrianPoints)
            {
                if (!PublicPedestrianArea(point) || IsPedestrianReserved(point, .8f) || IsCrowdProtected(point, .6f)) continue;
                foreach (var zone in map.zones)
                {
                    if (zone.kind != "office-workroom" || !zone.bounds.Contains(point)) continue;
                    var module = map.modules[zone.moduleIndex];
                    Vector2 along = (module.exit - module.entrance).normalized;
                    Vector2 side = new Vector2(along.y, -along.x);
                    Vector2 offset = point - zone.bounds.center;
                    float halfSide = Mathf.Abs(side.x) * zone.bounds.width * .5f + Mathf.Abs(side.y) * zone.bounds.height * .5f;
                    float halfAlong = Mathf.Abs(along.x) * zone.bounds.width * .5f + Mathf.Abs(along.y) * zone.bounds.height * .5f;
                    float lateral = Mathf.Abs(Vector2.Dot(offset, side));
                    // Public side edges are personal stopping places, not FileDesk delivery targets.
                    if (lateral >= halfSide - 2.5f && lateral <= halfSide - 1 && Mathf.Abs(Vector2.Dot(offset, along)) <= halfAlong - 2)
                    { coffeeDeskCandidates.Add(point); break; }
                }
            }
        }

        Vector2 ChooseCoffeeDesk(PersonV9 person)
        {
            BuildCoffeeDeskCandidates();
            int component = PedestrianComponent(person.position);
            Vector2 chosen = person.destination;
            float best = float.MaxValue;
            foreach (Vector2 point in coffeeDeskCandidates)
            {
                if (PedestrianComponent(point) != component) continue;
                float score = Vector2.Distance(person.position, point) + NextPedestrianRandom(person) * 5;
                foreach (var other in people)
                    if (other != person && other.coffeeAIInitialized)
                        score += Mathf.Max(0, 2.4f - Vector2.Distance(other.coffeeDesk, point)) * 80;
                if (score < best) { best = score; chosen = point; }
            }
            return chosen;
        }

        /// <summary>The generic chooser should return this result for initialized coffee coworkers.</summary>
        internal bool TryChooseCoffeeDestination(PersonV9 person)
        {
            if(person.authoredCoffee)return TryChooseAuthoredCoffeeDestination(person);
            if (!person.coffeeAIInitialized || person.kind != PersonKind.Coffee) return false;
            if (person.spilled)
            {
                if (person.coffeeState == CoffeeState.SpillReaction || person.coffeeState == CoffeeState.EmptyAtDesk) return false;
                person.coffeeState = CoffeeState.ReturningEmpty;
                person.coffeeGoingToDesk = true;
            }
            if (person.coffeeGoingToDesk)
                return AssignCoffeeTrip(person, person.coffeeDesk);

            int component = PedestrianComponent(person.position), best = -1;
            float bestScore = float.MinValue;
            for (int attempt = 0; attempt < 100 && pedestrianDestinations.Count > 0; attempt++)
            {
                int index = Mathf.Min(pedestrianDestinations.Count - 1, (int)(NextPedestrianRandom(person) * pedestrianDestinations.Count));
                Vector2 point = pedestrianDestinations[index];
                float fromDesk = Vector2.Distance(point, person.coffeeDesk), fromPerson = Vector2.Distance(point, person.position);
                if (fromDesk < 12 || fromDesk > 65 || fromPerson < 10 || PedestrianComponent(point) != component ||
                    IsPedestrianReserved(point, .7f) || IsCrowdProtected(point, .6f)) continue;
                float score = NextPedestrianRandom(person) * 12 - Mathf.Abs(fromDesk - 30) * .45f;
                if (score > bestScore) { bestScore = score; best = index; }
            }
            if (best >= 0 && AssignCoffeeTrip(person, pedestrianDestinations[best])) return true;
            person.coffeeGoingToDesk = true;
            return AssignCoffeeTrip(person, person.coffeeDesk);
        }

        bool AssignCoffeeTrip(PersonV9 person, Vector2 destination)
        {
            if (AssignPedestrianDestination(person, destination)) return true;
            // A temporary lack of a route never refills a cup, teleports, or selects a task station.
            person.aiState = PedestrianState.Waiting;
            person.waitLeft = 1;
            StopCoffeeCoworker(person);
            return false;
        }

        /// <summary>True means this frame's standing behavior is handled; player contact still applies.</summary>
        internal bool StepCoffeeAI(PersonV9 person, float dt)
        {
            if (!person.coffeeAIInitialized || person.kind != PersonKind.Coffee) return false;
            person.canRush = false;
            person.impatience = 0;
            person.coffeeGuardCooldown = Mathf.Max(0, person.coffeeGuardCooldown - dt);
            person.walkingSpeed = person.spilled ? person.coffeeCarrySpeed + .15f : person.coffeeCarrySpeed;

            // Contact() remains the sole writer of spilled and the coffee task count.
            if (person.spilled && person.coffeeState != CoffeeState.SpillReaction &&
                person.coffeeState != CoffeeState.ReturningEmpty && person.coffeeState != CoffeeState.EmptyAtDesk)
            {
                person.coffeeState = CoffeeState.SpillReaction;
                person.coffeeStateLeft = 1.1f;
                person.aiState = PedestrianState.Waiting;
                Log("coffee-spill-reaction", person.id);
            }

            if (person.coffeeState == CoffeeState.SpillReaction)
            {
                StopCoffeeCoworker(person);
                person.coffeeStateLeft -= dt;
                if (person.coffeeStateLeft <= 0)
                {
                    person.coffeeState = CoffeeState.ReturningEmpty;
                    person.coffeeGoingToDesk = true;
                    Log("coffee-empty-return", person.id);
                    TryChooseCoffeeDestination(person);
                }
                return true;
            }
            if (person.coffeeState == CoffeeState.EmptyAtDesk)
            {
                StopCoffeeCoworker(person);
                // Physical contact and crowd separation can displace a resting coworker.
                // Reuse the original personal destination; an empty cup never becomes full.
                if (Vector2.Distance(person.position, person.coffeeDesk) > .9f)
                {
                    person.coffeeState = CoffeeState.ReturningEmpty;
                    person.coffeeGoingToDesk = true;
                    Log("coffee-empty-return", person.id);
                    TryChooseCoffeeDestination(person);
                }
                return true;
            }
            if (person.coffeeState == CoffeeState.DeskPause)
            {
                StopCoffeeCoworker(person);
                person.coffeeStateLeft -= dt;
                if (person.coffeeStateLeft <= 0)
                {
                    person.coffeeState = CoffeeState.Carrying;
                    person.coffeeGoingToDesk = false;
                    TryChooseCoffeeDestination(person);
                }
                return true;
            }
            if (person.coffeeState == CoffeeState.GuardingCup)
            {
                StopCoffeeCoworker(person);
                person.coffeeStateLeft -= dt;
                if (person.coffeeStateLeft <= 0)
                {
                    person.coffeeState = CoffeeState.Carrying;
                    person.aiState = PedestrianState.Walking;
                    person.decisionLeft = 0;
                    person.coffeeGuardCooldown = .3f;
                }
                return true;
            }
            if (!person.spilled && person.coffeeGuardCooldown <= 0 && PlayerDashesTowardCoffee(person))
            {
                person.coffeeState = CoffeeState.GuardingCup;
                person.coffeeStateLeft = .55f;
                person.coffeeGuardCount++;
                person.aiState = PedestrianState.Waiting;
                StopCoffeeCoworker(person);
                Log("coffee-guard", person.id);
                return true;
            }
            return false;
        }

        bool PlayerDashesTowardCoffee(PersonV9 person)
        {
            if (!IsDashing || velocity.sqrMagnitude < 4) return false;
            Vector2 delta = person.position - player;
            if (delta.sqrMagnitude > 3.6f * 3.6f || delta.sqrMagnitude < .00001f) return false;
            Vector2 direction = velocity.normalized;
            if (Vector2.Dot(delta, direction) <= 0 || Mathf.Abs(delta.x * direction.y - delta.y * direction.x) > 1.15f) return false;
            return PedestrianLineClear(player, person.position, .1f);
        }

        /// <summary>Called at the usual physical arrival threshold, before the generic dwell.</summary>
        internal bool CoffeeArrival(PersonV9 person)
        {
            if (!person.coffeeAIInitialized || person.kind != PersonKind.Coffee) return false;
            PedestrianArrivals++;
            StopCoffeeCoworker(person);
            if (person.spilled)
            {
                person.coffeeState = CoffeeState.EmptyAtDesk;
                person.aiState = PedestrianState.Arrived;
                Log("coffee-empty-at-desk", person.id);
            }
            else if (person.coffeeGoingToDesk)
            {
                person.coffeeState = CoffeeState.DeskPause;
                person.coffeeStateLeft = 1.8f + NextPedestrianRandom(person) * 1.4f;
                person.coffeeDeskVisits++;
                person.aiState = PedestrianState.Arrived;
                Log("coffee-desk-pause", person.id);
            }
            else
            {
                person.coffeeGoingToDesk = true;
                TryChooseCoffeeDestination(person);
            }
            return true;
        }

        static void StopCoffeeCoworker(PersonV9 person)
        {
            person.velocity = person.steeringVelocity = Vector2.zero;
            person.blockedTime = 0;
            person.impatience = 0;
        }

        /// <summary>Steering comfort margin only. The actual collision radius is unchanged.</summary>
        internal float CoffeePersonalSpace(PersonV9 person)
        {
            if (!person.coffeeAIInitialized || person.kind != PersonKind.Coffee) return 0;
            if (person.spilled) return .08f;
            return person.coffeeState == CoffeeState.GuardingCup ? .34f : .22f;
        }
    }
}
