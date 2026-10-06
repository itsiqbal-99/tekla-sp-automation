using Tekla.Structures.Model;

namespace SinglePartAutoFix.Infrastructure.Tekla
{
    public class TeklaModelSession : ITeklaModelSession
    {
        private readonly Model _model;

        public TeklaModelSession()
        {
            _model = new Model();
        }

        public bool IsConnected()
        {
            return _model.GetConnectionStatus();
        }

        public string GetModelName()
        {
            return _model.GetInfo().ModelName;
        }

        public string GetModelPath()
        {
            return _model.GetInfo().ModelPath;
        }

        public Model GetModel()
        {
            return _model;
        }
    }
}
