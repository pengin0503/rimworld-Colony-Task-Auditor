using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using Verse.AI;
using RimWorld;
namespace Pengin.ColonyTaskAuditor
{
    // Timestamp+value CAS cannot distinguish an identical manual order in a paused tick.
    // Observe external writes as well; owned writes are explicitly scoped. A Clear is
    // attributed to vanilla only when it happens inside a known vanilla caller (#28).
    static class TaskPriorityHooks
    {
        static PriorityWork ownedWrite;
        public static bool Available {get;private set;}
        struct Frame {public PriorityWriteSource Source;public Pawn Pawn;}
        // Pushed by void prefixes (always run, per Harmony) and popped by finalizers (always run).
        static readonly List<Frame> context=new List<Frame>();
        static int timeoutTicks=-1;
        static bool clearWasExpired;
        public static void Install()
        {
            if(Available)return;
            try
            {
                Type h=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("HarmonyLib.Harmony",false)).FirstOrDefault(t=>t!=null);
                if(h==null)throw new InvalidOperationException("Harmony unavailable");
                var hm=h.Assembly.GetType("HarmonyLib.HarmonyMethod",true);
                var patch=h.GetMethods().First(m=>m.Name=="Patch"&&m.GetParameters().Length>=3&&m.GetParameters()[0].ParameterType==typeof(MethodBase));
                var instance=Activator.CreateInstance(h,new object[]{"pengin0503.colonytaskauditor.priority"});
                Func<string,object> hook=name=>Activator.CreateInstance(hm,new object[]{typeof(TaskPriorityHooks).GetMethod(name,BindingFlags.Public|BindingFlags.Static)});
                Action<MethodBase,object,object,object> apply=(target,prefix,postfix,finalizer)=>
                {
                    var args=new object[patch.GetParameters().Length];args[0]=target;args[1]=prefix;args[2]=postfix;
                    if(finalizer!=null){if(args.Length<5)throw new MissingMethodException("Harmony finalizer unsupported");args[4]=finalizer;}
                    patch.Invoke(instance,args);
                };
                var set=typeof(PriorityWork).GetMethod("Set",BindingFlags.Instance|BindingFlags.Public);
                var clear=typeof(PriorityWork).GetMethod("Clear",BindingFlags.Instance|BindingFlags.Public);
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                if(set==null||clear==null||typeof(PriorityWork).GetField("pawn",flags)==null||typeof(PriorityWork).GetField("prioritizedCell",flags)==null||typeof(PriorityWork).GetField("prioritizeTick",flags)==null)
                    throw new MissingMethodException("PriorityWork API changed");
                var timeout=typeof(PriorityWork).GetField("Timeout",BindingFlags.Static|BindingFlags.NonPublic);
                if(timeout!=null&&timeout.IsLiteral&&timeout.FieldType==typeof(int))timeoutTicks=(int)timeout.GetRawConstantValue();
                apply(set,null,hook("ExternalWrite"),null);
                apply(clear,hook("ClearPrefix"),hook("ExternalClear"),null);
                Available=true;
                // Optional attribution. A missing caller only makes its Clear count as External.
                var contexts=new List<KeyValuePair<MethodBase,string>>
                {
                    new KeyValuePair<MethodBase,string>(typeof(JobGiver_Work).GetMethod("TryIssueJobPackage",new[]{typeof(Pawn),typeof(JobIssueParams)}),"EnterNoWork"),
                    new KeyValuePair<MethodBase,string>(typeof(Pawn_MindState).GetMethod("Reset",new[]{typeof(bool),typeof(bool),typeof(bool)}),"EnterReset"),
                    new KeyValuePair<MethodBase,string>(typeof(Pawn_DraftController).GetProperty("Drafted")==null?null:typeof(Pawn_DraftController).GetProperty("Drafted").GetSetMethod(),"EnterDrafted"),
                    new KeyValuePair<MethodBase,string>(typeof(ThingCountTracker).GetMethod("InterruptEnroutePawns",new[]{typeof(Pawn)}),"EnterTargetChanged")
                };
                foreach(var entry in contexts)
                {
                    try
                    {
                        if(entry.Key==null||entry.Key.DeclaringType.Assembly!=typeof(Pawn).Assembly)throw new MissingMethodException(entry.Value);
                        apply(entry.Key,hook(entry.Value),null,hook("ExitContext"));
                    }
                    catch(Exception e){Log.Warning("Colony Task Auditor: vanilla priority-end attribution unavailable ("+entry.Value+"): "+e.GetType().Name);}
                }
            }
            catch(Exception e){Log.Warning("Colony Task Auditor priority observation unavailable: "+e.GetType().Name);}
        }
        static void Push(PriorityWriteSource source,Pawn pawn){context.Add(new Frame{Source=source,Pawn=pawn});}
        public static void EnterNoWork(Pawn pawn){Push(PriorityWriteSource.NoWork,pawn);}
        public static void EnterReset(Pawn_MindState __instance){Push(PriorityWriteSource.Reset,__instance==null?null:__instance.pawn);}
        public static void EnterDrafted(Pawn_DraftController __instance){Push(PriorityWriteSource.Drafted,__instance==null?null:__instance.pawn);}
        // Clears the priority of every pawn hauling to the removed target.
        public static void EnterTargetChanged(){Push(PriorityWriteSource.TargetChanged,null);}
        public static void ExitContext(){if(context.Count>0)context.RemoveAt(context.Count-1);}
        static PriorityWriteSource Context(Pawn pawn)
        {
            for(int i=context.Count-1;i>=0;i--)if(context[i].Pawn==null||context[i].Pawn==pawn)return context[i].Source;
            return PriorityWriteSource.Owned; // none
        }
        // Vanilla clears an expired order from inside IsPrioritized; read its state first.
        public static void ClearPrefix(IntVec3 ___prioritizedCell,int ___prioritizeTick)
        {
            clearWasExpired=false;
            try{clearWasExpired=timeoutTicks>0&&Scribe.mode==LoadSaveMode.Inactive&&___prioritizedCell.IsValid&&Find.TickManager!=null&&(long)Find.TickManager.TicksGame>=(long)___prioritizeTick+timeoutTicks;}
            catch(Exception){clearWasExpired=false;}
        }
        public static void ExternalWrite(PriorityWork __instance,Pawn ___pawn){Observe(__instance,___pawn,true,false);}
        public static void ExternalClear(PriorityWork __instance,Pawn ___pawn){bool expired=clearWasExpired;clearWasExpired=false;Observe(__instance,___pawn,false,expired);}
        static void Observe(PriorityWork work,Pawn pawn,bool isSet,bool expired)
        {
            if(ownedWrite==work||Scribe.mode!=LoadSaveMode.Inactive||pawn==null||pawn.Map==null)return;
            try
            {
                var source=TemporaryWorkPolicy.ClassifyWrite(false,isSet,expired,Context(pawn));
                pawn.Map.GetComponent<TaskActionState>().ExternalPriorityWrite(pawn,source);
            }
            catch(Exception){/* Observer must not change a manual command. */}
        }
        public static void Set(Pawn pawn,IntVec3 cell,WorkGiverDef giver)
        {var previous=ownedWrite;ownedWrite=pawn.mindState.priorityWork;try{ownedWrite.Set(cell,giver);}finally{ownedWrite=previous;}}
        public static void Clear(PriorityWork work)
        {var previous=ownedWrite;ownedWrite=work;try{work.Clear();}finally{ownedWrite=previous;}}
    }
}
