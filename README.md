Designed to be used with Natak API (https://github.com/TheAbysmalKraken/NatakAPI) to handle Catan game logic.

The version of NatakAPI used can be found here - https://github.com/Sharperrr/Natak_Edit
The edited API better supports MCTS agent development

To use this some edits are needed (and some optional):
- In Controllers/GameController.cs line 53 change logging file path to a valid path
- (Optional) In GameController.cs line 78-84 change the agents in the dictionary to asign the desired ones to a specific colour player (RandomAgent, RulesBasedAgent or MCTSAgent)
- (Optional) In UI/MainWindow.xaml.cs line 24 change player count to a desire number (3 or 4)
- (Optional)  In UI/GameBoard.xaml.cs line 22 change isDrawingEnabled to true/false to enable/disable drawing the game board (disabling it makes the games slightly faster)
- In Natak Edit Core/Natak.Infrastructure/GameStorageService.cs line 20 change _basePath to a valid folder
