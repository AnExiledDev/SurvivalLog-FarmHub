using System;
using System.Collections.Generic;

namespace FarmHub.Core;

/// <summary>
/// A fingerprint of everything one view is drawn from, taken the way that view rounds it: equal prints draw the same
/// view, so a read that found nothing new skips building the view (every pot planned against the presets) and sends
/// nothing. A growing crop's timer moves every read, but the HUD shows no timer for it, so only the window's print
/// follows it, at the tenth of an hour the window shows. Sets and the stock are taken in any order. The catalog, the
/// save and the preset book are compared by ViewCache itself, not printed.
/// </summary>
public static class FarmPrint
{
    public static ulong Hud(FarmWorld world, int rotWarnHours, bool fromPresets)
    {
        var print = new Print();

        print.Add(world.PlayerFloor);
        print.Add(HudView.StaminaShown(world));
        print.Add(world.ChoreCost);
        print.Add(rotWarnHours);
        print.Add(fromPresets);
        print.AddStock(world.Owned);
        print.AddNames(world.AreaNames);
        print.Add(world.Pots.Count);

        for (var i = 0; i < world.Pots.Count; i++)
        {
            var pot = world.Pots[i];

            print.AddPot(pot);
            print.Add(HudView.RotShown(pot));
            print.Add(FarmView.IsRottingSoon(pot, rotWarnHours));
            print.Add(world.ShowIds.TryGetValue(pot.Id, out var showId) ? showId : -1L);
        }

        return print.Value;
    }

    public static ulong Window(FarmWorld world, ViewSettings settings)
    {
        var print = new Print();

        print.Add(world.PlayerFloor);
        print.Add(settings.Key);
        print.Add(settings.RotWarnHours);
        print.Add(settings.IsKeepingRobotsStocked);
        print.AddStock(world.Owned);
        print.AddNames(world.AreaNames);
        print.Add(world.Pots.Count);

        for (var i = 0; i < world.Pots.Count; i++)
        {
            var pot = world.Pots[i];

            print.AddPot(pot);
            print.Add(pot.IsPowered);
            print.Add(FarmView.PercentShown(pot.Progress));
            print.Add(pot.State == PotState.Growing && pot.HoursToRipe is { } ripeIn ? FarmView.HoursShown(ripeIn) : (double?)null);
            print.Add(pot.State == PotState.Ripe && pot.HoursToRot is { } rotIn ? FarmView.HoursShown(rotIn) : (double?)null);
            print.Add(FarmView.IsRottingSoon(pot, settings.RotWarnHours));
        }

        print.Add(world.Robots.Count);

        for (var i = 0; i < world.Robots.Count; i++)
        {
            var robot = world.Robots[i];

            print.Add(robot.Id);
            print.Add(robot.Name);
            print.Add(robot.Floor);
            print.Add(FarmView.BatteryShown(robot));
            print.Add(robot.IsOn);
            print.Add(robot.Tiers.Count);

            for (var tier = 0; tier < robot.Tiers.Count; tier++)
            {
                print.Add(robot.Tiers[tier].Capacity);
                print.Add(robot.Tiers[tier].SeedId);
                print.Add(robot.Tiers[tier].FertId);
            }
        }

        return print.Value;
    }

    private struct Print
    {
        public ulong Value;

        public void Add(long value) => Value = Mix(Value * 31 + (ulong)value + 0x9E3779B97F4A7C15UL);

        public void Add(bool value) => Add(value ? 1L : 0L);

        public void Add(string? text) => Add(text == null ? -1L : StringComparer.Ordinal.GetHashCode(text));

        public void Add(double? value) => Add(value is { } number ? BitConverter.DoubleToInt64Bits(number) : long.MinValue);

        /// <summary>What the plan and both views read from a pot, all of it exact.</summary>
        public void AddPot(Pot pot)
        {
            Add(pot.Id);
            Add(pot.KindId);
            Add(pot.Floor);
            Add(pot.Area);
            Add((long)pot.State);
            Add(pot.PlantId);
            Add((long)pot.Problems);
            Add(pot.FertId);
            Add(pot.IsPending);
            Add(pot.RobotId);
            AddSet(pot.Unsuitable);
        }

        /// <summary>A set in any order: the sum of each id's mix, and the count.</summary>
        private void AddSet(IReadOnlySet<int> ids)
        {
            ulong sum = 0;

            if (ids is HashSet<int> hashSet)
            {
                foreach (var id in hashSet) sum += Mix((ulong)id + 0x632BE59BD9B4E019UL);
            }
            else
            {
                foreach (var id in ids) sum += Mix((ulong)id + 0x632BE59BD9B4E019UL);
            }

            Add(ids.Count);
            Add((long)sum);
        }

        public void AddStock(IReadOnlyDictionary<int, int> stock)
        {
            ulong sum = 0;

            if (stock is Dictionary<int, int> dictionary)
            {
                foreach (var pair in dictionary) sum += Mix(((ulong)(uint)pair.Key << 32 | (uint)pair.Value) + 0x632BE59BD9B4E019UL);
            }
            else
            {
                foreach (var pair in stock) sum += Mix(((ulong)(uint)pair.Key << 32 | (uint)pair.Value) + 0x632BE59BD9B4E019UL);
            }

            Add(stock.Count);
            Add((long)sum);
        }

        public void AddNames(IReadOnlyDictionary<int, string> names)
        {
            ulong sum = 0;

            if (names is Dictionary<int, string> dictionary)
            {
                foreach (var pair in dictionary) sum += Mix(((ulong)(uint)pair.Key << 32 | (uint)StringComparer.Ordinal.GetHashCode(pair.Value)) + 0x632BE59BD9B4E019UL);
            }
            else
            {
                foreach (var pair in names) sum += Mix(((ulong)(uint)pair.Key << 32 | (uint)StringComparer.Ordinal.GetHashCode(pair.Value)) + 0x632BE59BD9B4E019UL);
            }

            Add(names.Count);
            Add((long)sum);
        }

        /// <summary>splitmix64's finisher, so a one-bit change in any field reaches every bit of the print.</summary>
        private static ulong Mix(ulong x)
        {
            x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
            x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;

            return x ^ (x >> 31);
        }
    }
}
