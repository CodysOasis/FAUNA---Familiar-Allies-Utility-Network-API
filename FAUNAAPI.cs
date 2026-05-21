namespace FAUNA
{
    public class FaunaApi : IFaunaApi
    {
        private readonly FamiliarManager _manager;

        public FaunaApi(FamiliarManager manager)
        {
            _manager = manager;
        }

        public bool AddFamiliar(string familiarId, string customName = "")
            => _manager.AddFamiliar(familiarId, customName) != null;

        public void SpawnFamiliars()
            => _manager.SpawnFamiliars();
    }
}