namespace Shared.Messages;

/// <summary>
/// Événement publié par ProductsAPI après la création réussie d'un produit.
/// Contrat unique partagé par le publisher (ProductsAPI) et le consumer (OrdersAPI).
/// </summary>
/// <remarks>
/// Le PDF place ce contrat dans le namespace <c>Shared.Models</c>.
/// La structure cible de la mission impose <c>Shared/Messages</c> et le namespace
/// <c>Shared.Messages</c> : les propriétés du message sont identiques.
///
/// L'identifiant <see cref="Id"/> est l'identifiant métier stable du produit
/// (<c>Product.ProductId</c> dans ProductsAPI). Il sert de clé primaire à la
/// projection locale d'OrdersAPI et garantit l'idempotence : rejouer un message
/// avec le même <see cref="Id"/> ne crée pas de doublon.
/// Aucun identifiant d'événement supplémentaire n'est nécessaire dans le corps du
/// message : MassTransit fournit déjà un <c>MessageId</c> unique dans l'enveloppe
/// du transport.
/// </remarks>
public class ProductCreated
{
    /// <summary>Identifiant stable du produit (clé métier).</summary>
    public int Id { get; set; }

    /// <summary>Nom du produit au moment de la création.</summary>
    public string Name { get; set; } = string.Empty;
}
