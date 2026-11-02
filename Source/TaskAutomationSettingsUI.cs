using System;
using UnityEngine;
using Verse;
namespace Pengin.ColonyTaskAuditor
{
    public sealed class Window_TaskAutomationSettings : Window
    {
        Vector2 scroll;float height=1000;
        public Window_TaskAutomationSettings(){doCloseX=true;draggable=true;resizeable=true;}
        public override Vector2 InitialSize {get{return new Vector2(660,560);}}
        public override void DoWindowContents(Rect rect)
        {
            var s=TaskAuditorMod.Settings;
            Rect content=new Rect(0,0,rect.width-20,Math.Max(height,rect.height-30));
            Widgets.BeginScrollView(new Rect(rect.x,rect.y,rect.width,rect.height-30),ref scroll,content);
            var list=new Listing_Standard();list.Begin(content);
            list.Label("CTA_AutomationSafety".Translate());
            if(!TaskPriorityHooks.Available)list.Label("CTA_PriorityHookUnavailable".Translate());
            if(list.ButtonText("CTA_RemedyMode".Translate(("CTA_Mode"+s.RemedyMode).Translate())))s.RemedyMode=(ActionMode)(((int)s.RemedyMode+1)%3);
            if(list.ButtonText("CTA_AssistanceMode".Translate(("CTA_Mode"+s.AssistanceMode).Translate())))s.AssistanceMode=(ActionMode)(((int)s.AssistanceMode+1)%3);
            list.Label("CTA_WatchDefault".Translate((s.WatchThreshold/60000f).ToString("0.00")));s.WatchThreshold=(int)list.Slider(s.WatchThreshold,250,60000);
            list.Label("CTA_AssistanceDuration".Translate((s.AssistanceDuration/2500f).ToString("0.0")));s.AssistanceDuration=(int)list.Slider(s.AssistanceDuration,250,10000);
            list.Label("CTA_Cooldown".Translate((s.Cooldown/60000f).ToString("0.00")));s.Cooldown=(int)list.Slider(s.Cooldown,2500,180000);
            list.Label("CTA_MaxAssistance".Translate(s.MaxAssistance));s.MaxAssistance=(int)list.Slider(s.MaxAssistance,1,8);
            if(TemporaryWorkAssistance.PriorityConflict())list.Label("CTA_PriorityConflict".Translate());
            var map=Find.CurrentMap;
            if(map!=null)
            {
                var state=map.GetComponent<TaskActionState>();
                if(list.ButtonText("CTA_RevokeAll".Translate()))
                {foreach(var entry in state.Watches)entry.Info.AutoAssist=false;state.Revision++;}
                list.Label("CTA_ActionHistory".Translate());
                foreach(var entry in state.History)
                {
                    // Entries may end with a translation key (priority write source).
                    int reason=entry.LastIndexOf(" CTA_",System.StringComparison.Ordinal);
                    string key=reason<0?null:entry.Substring(reason+1);
                    list.Label(key==null||!key.CanTranslate()?entry:entry.Substring(0,reason+1)+key.Translate().ToString());
                }
            }
            height=list.CurHeight+20;list.End();Widgets.EndScrollView();
        }
        public override void PostClose(){base.PostClose();TaskAuditorMod.Instance.WriteSettings();}
    }
}
