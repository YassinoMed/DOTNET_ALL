using Xunit;

// Les tests d'intégration utilisent des variables d'environnement (chemin SQLite),
// ils doivent donc s'exécuter séquentiellement.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
