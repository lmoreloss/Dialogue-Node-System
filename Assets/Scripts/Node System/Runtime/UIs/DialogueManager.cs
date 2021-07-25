using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Project.NodeSystem 
{

    /// <summary>
    /// Le gestionnaire de dialogue. Charg� d'ex�cuter des actions en fonction du contenu des nodes d'un dialogueSO.
    /// </summary>
    public class DialogueManager : DialogueGetData
    {

        #region Fields

        [Tooltip("La langue du dialogue. Peut �tre modifi�e en cours de dialogue pour afficher une traduction � la prochaine r�plique.")]
        public LanguageType curLanguage = LanguageType.French;

        [Tooltip("L'interface visuelle du dialogue.")]
        DialogueUI dialogueUi;

        DialogueData curData;   //la DialogueNode en cours

        readonly List<DialogueButtonContainer> dialogueButtonContainers = new List<DialogueButtonContainer>();
        readonly List<NodeData_BaseContainer> baseContainers = new List<NodeData_BaseContainer>();
        private int curIndex = 0;


        #endregion


        #region Dialogue

        void Start()
        {
            //On affiche l'interface de dialogue
            dialogueUi = GetComponent<DialogueUI>();

            StartDialogue();
        }

        /// <summary>
        /// R�cup�re la node de d�part.
        /// </summary>
        public void StartDialogue()
        {
            dialogueUi.StartDialogue();

            //A faire : R�cup�rer plusieurs StartNodes et s�lectionner celle � suivre en fonction de ses conditions.
            //La raison pour laquelle on peut avoir plusieurs StartNodes est dans le cas o� l'on veut choisir une node
            //de d�part en fonction de certaines conditions (Une BranchNode ne peut prendre que 2 chemins, "true" ou "false").
            CheckNodeType(GetNextNode(dialogue.startDatas[0]));
        }


        /// <summary>
        /// R�cup�re l'action � ex�cuter en fonction du type de node atteint.
        /// </summary>
        /// <param name="inputData">La node � convertir.</param>
        private void CheckNodeType(BaseData inputData)
        {
            switch (inputData)
            {
                case StartData nodeData:
                    RunNode(nodeData);
                    break;
                case DialogueData nodeData:
                    RunNode(nodeData);
                    break;
                case BranchData nodeData:
                    RunNode(nodeData);
                    break;
                case ChoiceData nodeData:
                    RunNode(nodeData);
                    break;
                case EventData nodeData:
                    RunNode(nodeData);
                    break;
                case EndData nodeData:
                    RunNode(nodeData);
                    break;
                default:
                    break;
            }
        }


        #endregion



        private void RunNode(StartData startNodeData)
        {
            //C'est une StartNode, on n'a rien � faire. On r�cup�re la node suivante.
            CheckNodeType(GetNextNode(dialogue.startDatas[0]));
        }

        private void RunNode(BranchData nodeData)
        {
            bool checkBranch = true;
            foreach (EventData_StringCondition item in nodeData.stringConditions)
            {
                //Pour chaque condition de la BranchNode, on regarde si la valeur du DE_Trigger et celle de la node correspondent
                if (!GameEvents.DialogueConditionEvents(item.stringEvent.value, item.conditionType.value, item.number.value))
                {
                    checkBranch = false;
                    break;
                }
            }

            string nextNode = (checkBranch ? nodeData.trueNodeGuid : nodeData.falseNodeGuid);
            CheckNodeType(GetNodeByGuid(nextNode));
        }

        private void RunNode(ChoiceData nodeData)
        {
            //On doit maintenant r�cup�rer les choix de la node et leurs conditions
            //pour les assigner aux boutons de l'interface
            AssignActionsToButtons(nodeData);
        }

        private void RunNode(EventData nodeData)
        {
            //Si on a un DialogueEvent � jouer, celui-ci va appeler la classe statique GameEvents
            //pour lier l'event au DE_Trigger en question
            foreach (ContainerValue<DialogueEventSO> item in nodeData.scriptableEvents)
            {
                if (item.value != null)
                {
                    item.value.Invoke();
                }
            }
            //Pour chaque stringEvent, on r�cup�re la variable correspondante dans DE_Trigger et on la modifie selon le modifierType
            foreach (EventData_StringModifier item in nodeData.stringEvents)
            {
                GameEvents.DialogueModifierEvents(item.stringEvent.value, item.modifierType.value, item.number.value);
            }
            CheckNodeType(GetNextNode(nodeData));
        }

        private void RunNode(EndData nodeData)
        {
            dialogueUi.EndDialogue();
        }

        private void RunNode(DialogueData nodeData)
        {
            //Cache la node pour l'utiliser plus tard
            curData = nodeData;

            /* Les conteneurs de la DialogueNode (Persos et textes) peuvent changer d'ordre d'ex�cution selon leur ID.
             * On s'assure de les r�cup�rer dans le bon ordre avant de les analyser.
             */
            baseContainers.Clear();
            baseContainers.AddRange(nodeData.characters);
            baseContainers.AddRange(nodeData.repliques);

            curIndex = 0;

            baseContainers.Sort(delegate (NodeData_BaseContainer x, NodeData_BaseContainer y)
            {
                return x.ID.value.CompareTo(y.ID.value);
            });

            DisplayDialogueAndCharacters(nodeData);
        }





        private void DisplayDialogueAndCharacters(DialogueData nodeData)
        {
            //Pour chaque conteneur de la DialogueNode, on regarde s'il s'agit d'un perso ou d'une r�plique
            for (int i = curIndex; i < baseContainers.Count; i++)
            {
                curIndex = i + 1;
                if (baseContainers[i] is DialogueData_CharacterSO)
                {
                    //Si c'est un perso, on regarde s'il a un SO ou pas
                    DialogueData_CharacterSO tmp = baseContainers[i] as DialogueData_CharacterSO;
                    if(tmp.character.value == null)
                    {
                        //Si on n'a pas de personnage, on n'affiche pas de perso � l'emplacement "sidePlacement"
                        dialogueUi.SetCharacter("", new Color(252, 3, 252, 1), null, tmp.faceDirection.value, tmp.sidePlacement.value);
                    }
                    else
                    {
                        dialogueUi.SetCharacter(tmp.characterName.value, tmp.character.value.characterNameColor, tmp.sprite.value, tmp.faceDirection.value, tmp.sidePlacement.value);
                    }
                }
                if (baseContainers[i] is DialogueData_Repliques)
                {
                    //Si c'est une r�plique, on r�cup�re son texte et son audio en fonction de la langue...
                    DialogueData_Repliques tmp = baseContainers[i] as DialogueData_Repliques;
                    dialogueUi.SetText(tmp.texts.Find(text => text.language == curLanguage).data);
                    dialogueUi.PlaySound(tmp.audioClips.Find(text => text.language == curLanguage).data);

                    break;
                }


                //Quand on a fini de parcourir la node, on passe � la suivante
                if (curIndex == baseContainers.Count-1)
                {
                    UnityAction displayNextNode = () => CheckNodeType(GetNextNode(nodeData));
                    dialogueUi.SetContinueBtn(displayNextNode);
                }
                //Sinon, on continue de d�filer les r�pliqus
                else
                {
                    UnityAction redo = () => DisplayDialogueAndCharacters(nodeData);
                    dialogueUi.SetContinueBtn(redo);
                }
            }
        }



        private void AssignActionsToButtons(ChoiceData nodeData)
        {

            // Assigne les fonctions du bouton Continuer et des boutons de choix.
            // La ChoiceNode s'assure qu'on ait au moins 1 choix disponible pour �viter les bugs.
            // Sinon, on parcourt la liste


            dialogueButtonContainers.Clear();

            //On v�rifie qu'il y a au moins un choix de connect�
            bool hasChoices = false;

            foreach (ChoiceData_Container container in nodeData.choices)
            {
                //Si le port de choix est bien connect�, on peut ajouter le choix � la liste
                if (!string.IsNullOrEmpty(container.linkedPort.inputGuid))
                {
                    AssignChoice(container);
                    hasChoices = true;
                }
            }

            //Si on a des choix, on affiche le panel des choix.
            if (hasChoices)
            {
                dialogueUi.SetChoices(dialogueButtonContainers);
                dialogueUi.ShowChoicesPanel();
            }
            //Sinon, la ChoiceNode n'a pas de points de sortie.
            //Dans ce cas, pour �viter les erreurs, on arr�te le dialogue.
            else
            {
                dialogueUi.EndDialogue();
            }


            
        }


        private void AssignChoice(ChoiceData_Container choice)
        {
            bool checkBranch = true;
            foreach (ChoiceData_Condition condition in choice.conditions)
            {
                if (!GameEvents.DialogueConditionEvents(condition.stringCondition.stringEvent.value, condition.stringCondition.conditionType.value, condition.stringCondition.number.value))
                {
                    checkBranch = false;
                    break;
                }
            }

            //On cr�e un DialogueButtonContainer, qui indiquera au UI si un choix doit �tre disponible ou pas
            UnityAction onChoiceClicked = () => CheckNodeType(GetNodeByGuid(choice.linkedPort.inputGuid));

            DialogueButtonContainer dialogueButtonContainer = new DialogueButtonContainer();

            dialogueButtonContainer.ChoiceState = choice.choiceStateType.value;

            //Pour le texte du choix, on met le texte de base + les descriptions si les conditions ne sont pas remplies.
            dialogueButtonContainer.Text = choice.texts.Find(text => text.language == curLanguage).data;
            if (!checkBranch)
            {
                foreach (ChoiceData_Condition condition in choice.conditions)
                {
                    string desc = condition.descriptionsIfNotMet.Find(desc => desc.language == curLanguage).data;

                    if (!string.IsNullOrEmpty(desc))
                    {
                        dialogueButtonContainer.Text = string.Format("{0}   <size=30><i><color=#FF999999>({1})</color></i></size>",
                                                                     dialogueButtonContainer.Text,
                                                                     desc);
                    }
                }
            }


            dialogueButtonContainer.OnChoiceClicked = onChoiceClicked;
            dialogueButtonContainer.ConditionCheck = checkBranch;

            dialogueButtonContainers.Add(dialogueButtonContainer);
        }
    }
}
