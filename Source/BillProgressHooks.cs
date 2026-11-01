using System;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;
namespace Pengin.ColonyTaskAuditor
{
    // One postfix observes completed production iterations, including unlimited bills
    // that start and finish between scans. It never changes the result or work order.
    static class BillProgressHooks
    {
        static bool installed;
        public static bool Available {get{return installed;}}
        public static void Install()
        {
            if(installed)return;
            try
            {
                Type h=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("HarmonyLib.Harmony",false)).FirstOrDefault(t=>t!=null);
                if(h==null)throw new InvalidOperationException("Required Harmony assembly unavailable");
                Type hm=h.Assembly.GetType("HarmonyLib.HarmonyMethod",true);
                object instance=Activator.CreateInstance(h,new object[]{"pengin0503.colonytaskauditor.progress"});
                MethodInfo patch=h.GetMethods().First(m=>m.Name=="Patch"&&m.GetParameters().Length>=3&&m.GetParameters()[0].ParameterType==typeof(MethodBase));
                var args=new object[patch.GetParameters().Length];
                args[0]=typeof(Bill_Production).GetMethod("Notify_IterationCompleted",new[]{typeof(Pawn),typeof(System.Collections.Generic.List<Thing>)});
                if(args[0]==null)throw new MissingMethodException("Bill completion signature changed");
                args[2]=Activator.CreateInstance(hm,new object[]{typeof(BillProgressHooks).GetMethod("Completed",BindingFlags.Static|BindingFlags.Public)});
                patch.Invoke(instance,args);installed=true;
            }
            catch(Exception e){Log.Warning("Colony Task Auditor completion observation unavailable: "+e.GetType().Name);}
        }
        public static void Completed(Bill_Production __instance)
        {
            try
            {
                Thing giver=__instance.billStack==null?null:__instance.billStack.billGiver as Thing;
                if(giver!=null&&giver.Map!=null)giver.Map.GetComponent<TaskAuditComponent>().NotifyBillProgress(__instance);
            }
            catch(Exception){/* An observer must never interrupt a completed recipe. */}
        }
    }
}
