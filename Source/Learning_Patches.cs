using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ImpressionableChildren {
    [HarmonyPatch]
    public static class Learning_Patches {
        private class LearningConversion {
            public const int   Hour          = 2500;
            public const int   MaxTicks      = 2 * Hour;      // 2h
            public const float ChangePerTick = 0.005f / Hour; // 0.5 %/h

            public int ticks = 0;
            public readonly Pawn pupil;
            public Pawn teacher;
            public Job job;

            public LearningConversion(Pawn teacher, Pawn pupil, Job job) {
                this.teacher = teacher;
                this.pupil = pupil;
                if (teacher == null) {
                    this.job = job;
                }
            }

            public void End() {
                Apply();
                Remove();
            }

            private void Remove() {
                conversions.Remove(pupil);
            }

            public void Apply() {
                if (teacher == null) return;
                if (pupil.Ideo == teacher.Ideo) {
                    Remove(); // Ideo changed some other way.
                }
                float change = ticks * ChangePerTick
                    * teacher.GetStatValue(StatDefOf.SocialImpact)
                    * pupil.GetStatValue(StatDefOf.CertaintyLossFactor);
                pupil.ideo.IdeoConversionAttempt(change, teacher.Ideo);
                ticks = 0;
            }

            public void Tick() {
                ticks++;
                if (teacher == null) {
                    teacher = (Pawn) job.targetB.Thing;
                    if (teacher != null) {
                        job = null;
                        if (teacher.Ideo == pupil.Ideo) {
                            Remove(); // Correct ideo already
                        }
                    }
                }
                if (ticks >= MaxTicks) {
                    Apply();
                }
            }
        }

        private static readonly Dictionary<Pawn,LearningConversion> conversions =
            new Dictionary<Pawn,LearningConversion>();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(JobDriver_Lessontaking), "MakeNewToils")]
        public static void MakeNewToils(JobDriver_Lessontaking __instance) {
            Pawn teacher = (Pawn) __instance.job.targetB.Thing;
            Pawn pupil = __instance.pawn;
            if (teacher?.Ideo != pupil.Ideo) {
                if (conversions.ContainsKey(pupil)) {
                    conversions[pupil].job = __instance.job;
                    conversions[pupil].teacher = teacher;
                } else {
                    conversions[pupil] = new LearningConversion(teacher, pupil, __instance.job);
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(LearningUtility), nameof(LearningUtility.LearningTickCheckEnd))]
        public static void LearningTickCheckEnd(Pawn pawn, bool __result) {
            if (conversions.ContainsKey(pawn)) {
                conversions[pawn].Tick();
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.EndCurrentJob))]
        public static void EndCurrentJob(Pawn ___pawn, JobDriver ___curDriver) {
            Pawn pupil = ___pawn;
            if (___curDriver is JobDriver_Lessontaking && conversions.ContainsKey(pupil)) {
                conversions[pupil].End();
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Pawn_IdeoTracker), nameof(Pawn_IdeoTracker.IdeoConversionAttempt))]
        public static void IdeoConversionAttempt(ref bool applyCertaintyFactor) {
            applyCertaintyFactor = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Pawn), nameof(Pawn.ExposeData))]
        public static void ExposeData(ref Pawn __instance) {
            Pawn pawn = __instance;
            LearningConversion state = null;
            if (conversions.ContainsKey(pawn)) {
                state = conversions[pawn];
            } else if (Scribe.mode == LoadSaveMode.LoadingVars) {
                state = new LearningConversion(null, pawn, null);
                conversions[pawn] = state;
            } else {
                return;
            }

            if (Scribe.EnterNode(Strings.SaveNode)) {
                Scribe_Values.Look(ref state.ticks, "ticks");
                Scribe_References.Look(ref state.job, "job");
                Scribe_References.Look(ref state.teacher, "teacher");
                Scribe.ExitNode();
            } else if (Scribe.mode == LoadSaveMode.LoadingVars) {
                conversions.Remove(pawn);
            }
        }
    }
}
