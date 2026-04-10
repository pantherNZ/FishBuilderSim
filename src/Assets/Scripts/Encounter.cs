public class Encounter
{
    public int EncounterNumber;
    public SpeciesGroup EnemyGroup;
    public bool IsCompleted;
    public bool PlayerWon;

    public Encounter(int encounterNumber, SpeciesGroup enemyGroup)
    {
        EncounterNumber = encounterNumber;
        EnemyGroup = enemyGroup;
    }
}
