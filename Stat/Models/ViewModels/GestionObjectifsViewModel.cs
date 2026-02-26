// [Fichier: Models/ViewModels/GestionObjectifsViewModel.cs]
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace Stat.Models.ViewModels
{
    // Modèle principal pour la page de gestion des objectifs
    public class GestionObjectifsViewModel
    {
        // --- C'EST LA LIGNE QUI DOIT ÊTRE CORRIGÉE ---
        // Assurez-vous que cette ligne utilise bien le type "ObjectifListItemViewModel"
        public List<ObjectifListItemViewModel> Objectifs { get; set; }

        public List<SelectListItem> AxesOptions { get; set; }
    }

    // Représente un seul objectif dans la liste de gestion
    public class ObjectifListItemViewModel
    {
        public int IdObjectif { get; set; }
        public string IntituleObjectif { get; set; }
        public string AxeName { get; set; }
    }
}