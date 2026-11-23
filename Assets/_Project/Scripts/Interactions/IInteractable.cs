namespace NED2024.Interactions
{
    /// <summary>Anything the player can use with the E key.</summary>
    public interface IInteractable
    {
        string Prompt { get; }
        bool CanInteract { get; }
        void Interact();
    }
}
