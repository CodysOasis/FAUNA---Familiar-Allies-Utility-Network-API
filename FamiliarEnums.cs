namespace FAUNA
{
    public enum FamiliarState
    {
        Passive,
        Following
    }

        public enum FamiliarAbilityState
        {
            None,
            WalkingToTarget,
            WalkingToPosition,
            HittingTarget, 
            ReturningToPlayer
        }

    public enum FamiliarForm
    {
        Animal,
        Humanoid
    }
        public enum DietType
        {
            Carnivore,   // Eggs, Milk, + Meat if Animal Husbandry installed
            Herbivore,   // Fruits, Vegetables
            Omnivore     // All of the above
        }
        
}