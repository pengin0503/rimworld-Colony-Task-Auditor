using System.Collections.Generic;

namespace Pengin.ColonyTaskAuditor
{
    // A shortage in spawned stocks is only likely: inventories and deliveries may exist.
    public static class BlockerEvidence
    {
        public static string Materials(int undelivered,int spawnedAvailable)
        {
            if(undelivered<=0)return null;
            return spawnedAvailable<undelivered?"CTA_MaterialMissing":"CTA_MaterialDelivery";
        }
        // All cost entries are inspected: any shortage outranks "not yet delivered",
        // independent of the order in which the recipe lists its materials.
        public static string Materials(IEnumerable<KeyValuePair<int,int>> neededAndStock)
        {
            bool pending=false;
            foreach(var entry in neededAndStock)
            {
                if(entry.Key<=0)continue;
                if(entry.Value<entry.Key)return "CTA_MaterialMissing";
                pending=true;
            }
            return pending?"CTA_MaterialDelivery":null;
        }
        public static string Reservation(bool reserved,bool workerCanReserve)
        {
            return reserved&&!workerCanReserve?"CTA_Reserved":null;
        }
    }
}
