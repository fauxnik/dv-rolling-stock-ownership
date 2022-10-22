using DV.Logic.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DVOwnership
{
    public class ProceduralJobGenerator
    {
        private StationController stationController;
        private StationProceduralJobsRuleset generationRuleset;
        private YardTracksOrganizer yto;

        public ProceduralJobGenerator(StationController stationController)
        {
            this.stationController = stationController;
            generationRuleset = stationController.proceduralJobsRuleset;
            yto = SingletonBehaviour<YardTracksOrganizer>.Instance;
        }

        public JobChainController GenerateHaulChainJobForCars(List<Car> carsForJob)
        {

            throw new NotImplementedException();
        }

        public JobChainController GenerateUnloadChainJobForCars(List<Car> carsForJob)
        {

            throw new NotImplementedException();
        }

        public JobChainController GenerateLoadChainJobForCars(List<Car> carsForJob)
        {

            throw new NotImplementedException();
        }
    }
}
