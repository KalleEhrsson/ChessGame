using UnityEngine;

[DisallowMultipleComponent]
public class PromotionPieceOption : MonoBehaviour
{
    public PieceType PromotionType { get; private set; }
    public bool IsGhost { get; private set; }

    PromotionSelection3DController owner;

    public void Initialize(PromotionSelection3DController optionOwner, PieceType promotionType, bool isGhost)
    {
        owner = optionOwner;
        PromotionType = promotionType;
        IsGhost = isGhost;
    }

    public void NotifyClicked()
    {
        owner?.HandleOptionClicked(this);
    }
}
