using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NeoCortexApiSample
{
    public class Sample
    {
        public Sample()
        {
            this.Feature = new Dictionary<string, object>();
        }

        public Dictionary<string, object> Feature { get; set; }

        public List<string> FeatureNames {
            get 
            {
                return Feature.Keys.ToList();
            }
        }
    }
}
