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
        public static string Reservation(bool reserved,bool workerCanReserve)
        {
            return reserved&&!workerCanReserve?"CTA_Reserved":null;
        }
    }
}
