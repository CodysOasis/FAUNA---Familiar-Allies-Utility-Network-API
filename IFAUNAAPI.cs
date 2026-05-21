namespace FAUNA
{
public interface IFaunaApi
{
    bool AddFamiliar(string familiarId, string customName = "");
    void SpawnFamiliars();
}
}