using System;
using System.Collections.Generic;
using System.Reflection;
using SWM;

namespace SWM.RegressionTests
{
    internal static class Program
    {
        private static readonly MethodInfo RemoveIndexRefMethod =
            typeof(Parameters).GetMethod(
                "RemoveIndexRef",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static int Main()
        {
            if (RemoveIndexRefMethod == null)
            {
                Console.Error.WriteLine("FAIL: RemoveIndexRef method was not found.");
                return 1;
            }

            try
            {
                KeepsActiveTriggerThatCoversDownstreamImage();
                RemovesLeafTriggerAndTransfersCoverage();
                Console.WriteLine("PASS: RemoveIndexRef regression tests.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FAIL: {ex.Message}");
                return 1;
            }
        }

        private static void KeepsActiveTriggerThatCoversDownstreamImage()
        {
            var activeTriggers = new Dictionary<int, Dictionary<int, int>>
            {
                [65] = new Dictionary<int, int> { [60] = 70 },
                [75] = new Dictionary<int, int> { [65] = 70 }
            };
            var inspected = new bool[100];
            var modes = new int[100];

            // Trigger 65 already covers its ref2 (image 70).
            modes[65] = 2;

            bool removed = InvokeRemoveIndexRef(
                75,
                65,
                1,
                ref activeTriggers,
                ref inspected,
                ref modes);

            Assert(!removed, "Trigger 65 must not report removal.");
            Assert(
                activeTriggers.ContainsKey(65),
                "Trigger 65 was removed even though it still covers image 70.");
            Assert(
                modes[75] == 0,
                "Trigger 75 must not take over Trigger 65 while 65 has dependents.");
        }

        private static void RemovesLeafTriggerAndTransfersCoverage()
        {
            var activeTriggers = new Dictionary<int, Dictionary<int, int>>
            {
                [65] = new Dictionary<int, int> { [60] = 70 },
                [75] = new Dictionary<int, int> { [65] = 70 }
            };
            var inspected = new bool[100];
            var modes = new int[100];

            bool removed = InvokeRemoveIndexRef(
                75,
                65,
                1,
                ref activeTriggers,
                ref inspected,
                ref modes);

            Assert(removed, "A leaf Trigger must report removal.");
            Assert(
                !activeTriggers.ContainsKey(65),
                "Leaf Trigger 65 must be removed after Trigger 75 takes it over.");
            Assert(inspected[65], "Transferred image 65 must be marked covered.");
            Assert(modes[75] == 1, "Trigger 75 must enable ref1 coverage.");
        }

        private static bool InvokeRemoveIndexRef(
            int index,
            int indexRef,
            int refFlag,
            ref Dictionary<int, Dictionary<int, int>> activeTriggers,
            ref bool[] inspected,
            ref int[] modes)
        {
            object[] arguments =
            {
                index,
                indexRef,
                refFlag,
                activeTriggers,
                inspected,
                modes
            };

            bool result = (bool)RemoveIndexRefMethod.Invoke(
                new Parameters(),
                arguments);

            activeTriggers =
                (Dictionary<int, Dictionary<int, int>>)arguments[3];
            inspected = (bool[])arguments[4];
            modes = (int[])arguments[5];

            return result;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
