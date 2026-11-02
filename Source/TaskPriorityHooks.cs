using System;
using System.Linq;
using System.Reflection;
using Verse;
using RimWorld;
namespace Pengin.ColonyTaskAuditor
{
    // Timestamp+value CAS cannot distinguish an identical manual order in a paused tick.
    // Observe external writes as well; owned writes are explicitly scoped.
    static class TaskPriorityHooks
    {
        static PriorityWork ownedWrite;
        public static bool Available {get;private set;}
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
                foreach(string name in new[]{"Set","Clear"})
                {
                    var target=typeof(PriorityWork).GetMethod(name,BindingFlags.Instance|BindingFlags.Public);
                    if(target==null||typeof(PriorityWork).GetField("pawn",BindingFlags.Instance|BindingFlags.NonPublic)==null)throw new MissingMethodException("PriorityWork API changed");
                    var args=new object[patch.GetParameters().Length];args[0]=target;
                    args[2]=Activator.CreateInstance(hm,new object[]{typeof(TaskPriorityHooks).GetMethod("ExternalWrite",BindingFlags.Public|BindingFlags.Static)});
                    patch.Invoke(instance,args);
                }
                Available=true;
            }
            catch(Exception e){Log.Warning("Colony Task Auditor priority observation unavailable: "+e.GetType().Name);}
        }
        public static void ExternalWrite(PriorityWork __instance,Pawn ___pawn)
        {
            if(ownedWrite==__instance||Scribe.mode!=LoadSaveMode.Inactive||___pawn==null||___pawn.Map==null)return;
            try{___pawn.Map.GetComponent<TaskActionState>().ExternalPriorityWrite(___pawn);}catch(Exception){/* Observer must not change a manual command. */}
        }
        public static void Set(Pawn pawn,IntVec3 cell,WorkGiverDef giver)
        {var previous=ownedWrite;ownedWrite=pawn.mindState.priorityWork;try{ownedWrite.Set(cell,giver);}finally{ownedWrite=previous;}}
        public static void Clear(PriorityWork work)
        {var previous=ownedWrite;ownedWrite=work;try{work.Clear();}finally{ownedWrite=previous;}}
    }
}
