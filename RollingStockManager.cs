using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace DVCareer
{
    internal class RollingStockManager : SingletonBehaviour<RollingStockManager>
    {
        private List<Equipment> equipment = new List<Equipment>();

        public static new string AllowAutoCreate() { return "DVCareer_RollingStockManager"; }

        public void LoadSaveData(JArray data)
        {
            foreach(var token in data)
            {
                if (token.Type != JTokenType.Object) { continue; }

                equipment.Add(Equipment.FromSaveData((JObject)token));
            }
        }

        public JArray GetSaveData() { return new JArray(from eq in equipment select eq.GetSaveData()); }
    }
}
