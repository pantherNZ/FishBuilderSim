public class Encounter
{
    public SpeciesGroup EnemyGroup;
    public bool IsCompleted;
    public bool PlayerWon;

    public Encounter(SpeciesGroup enemyGroup)
    {
        EnemyGroup = enemyGroup;
    }
}
