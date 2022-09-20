using SharedGame;
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using SepM.Physics;
using System.Data;

namespace SimpPlatformer {

    // TODO: Is this explicit reference necessary?
    using static VWConstants;
    // using static SepM

    // TODO: Use separate Constants file under the same namespace
    public static class VWConstants {
        public const int MAX_SHIPS = 4;
        public const int MAX_PLAYERS = 64;
        //public const float PI = 3.1415926f;
    }

    [Serializable]
    public struct Bullet {
        public bool active;
        public Vector2 position;
        public Vector2 velocity;

        public void Serialize(BinaryWriter bw) {
            bw.Write(active);
            bw.Write(position.x);
            bw.Write(position.y);
            bw.Write(velocity.x);
            bw.Write(velocity.y);
        }

        public void Deserialize(BinaryReader br) {
            active = br.ReadBoolean();
            position.x = br.ReadSingle();
            position.y = br.ReadSingle();
            velocity.x = br.ReadSingle();
            velocity.y = br.ReadSingle();
        }
    };

    [Serializable]
    public class Box {
        public int instanceId;
        public Vector3Int position;

        public void Serialize(BinaryWriter bw) {
            bw.Write(instanceId);
            bw.Write(position.x);
            bw.Write(position.y);
            bw.Write(position.z);
        }

        public void Deserialize(BinaryReader br) {
            instanceId = br.ReadInt32();
            position.x = br.ReadInt32();
            position.y = br.ReadInt32();
            position.z = br.ReadInt32();
        }

        public override int GetHashCode() {
            int hashCode = 1858537542;
            hashCode = hashCode * -1521134295 + instanceId.GetHashCode();
            hashCode = hashCode * -1521134295 + position.GetHashCode();
            return hashCode;
        }
    }

    [Serializable]
    public struct SpGame : IGame {
        // TODO: Keep track of PhysObject/GameObject/id map (3-tuple?)
            // Yeah, start with 3-tuple and if it becomes redundant, make it smaller
        Dictionary<int, GameObject> objectIDMap;
        public int Framenumber { get; private set; }
        fp timestep;
        int currentLevel;
        public static int endLevel;

        public int Checksum => GetHashCode();

        public Box[] _boxes;
        public PhysWorld _world;
        // TODO: How to manage this?
        public Character[] _characters;

        // TODO: Just pass in PhysWorld and Character. Should be enough

        public static Rect _bounds = new Rect(0, 0, 640, 480);

        public void Serialize(BinaryWriter bw) {
            // Frame
            bw.Write(Framenumber);
            // Boxes
            bw.Write(_boxes.Length);
            for (int i = 0; i < _boxes.Length; ++i) {
                _boxes[i].Serialize(bw);
            }
            // World
            _world.Serialize(bw);
            // Level
            bw.Write(currentLevel);
        }

        public void Deserialize(BinaryReader br) {
            // Frame
            Framenumber = br.ReadInt32();
            // Boxes
            int box_length = br.ReadInt32();
            if (box_length != _boxes.Length) {
                _boxes = new Box[box_length];
            }
            for (int i = 0; i < _boxes.Length; ++i) {
                _boxes[i].Deserialize(br);
            }
            // World
            _world.Deserialize(br);
            // Level
            currentLevel = br.ReadInt32();
        }

        /* Gets called on shutdown */
        public void CleanUp(){
            //Destroy all gameobjects
            foreach(KeyValuePair<int, GameObject> kvp in objectIDMap){
                GameObject g = kvp.Value;
                GameObject.Destroy(g);
            }
            objectIDMap.Clear();

            //Clean up world
            _world.CleanUp();
        }

        public NativeArray<byte> ToBytes() {
            using (var memoryStream = new MemoryStream()) {
                using (var writer = new BinaryWriter(memoryStream)) {
                    Serialize(writer);
                }
                return new NativeArray<byte>(memoryStream.ToArray(), Allocator.Persistent);
            }
        }

        public void FromBytes(NativeArray<byte> bytes) {
            using (var memoryStream = new MemoryStream(bytes.ToArray())) {
                using (var reader = new BinaryReader(memoryStream)) {
                    Deserialize(reader);
                }
            }
        }

        /* Determines direction number based on movement inputs*/
        private int GetDirNumber(int v, int h){
            if(h > 0){
                if(v > 0)
                    return 9; //Up-right
                if (v < 0)
                    return 3; //Down-right
                return 6; //Right
            }
            if(h < 0){
                if(v > 0)
                    return 7; //Up-left
                if (v < 0)
                    return 1; //Down-left
                return 4; //Left
            }
            if(v > 0)
                return 8; //Up
            if (v < 0)
                return 2; //Down
            return 5; //Neutral
        }

        private GameObject GetObjectFromID(int id){
            GameObject result = null;
            objectIDMap.TryGetValue(id, out result);
            if(result == null)
                Debug.Log("GO not found.");
            return result;
        }

        //private static float DegToRad(float deg) {
        //    return PI * deg / 180;
        //}

        //private static float Distance(Vector2 lhs, Vector2 rhs) {
        //    float x = rhs.x - lhs.x;
        //    float y = rhs.y - lhs.y;
        //    return Mathf.Sqrt(x * x + y * y);
        //}

        private bool CheckInput(long currentInputs, uint input){
            return (currentInputs & input) != 0;
        }

        /* Takes a direction number (1-9) and returns a Vector2 representing that direction. */
        private Vector2Int GetVectorFromDirNumber(int dirNum){
            Vector2Int result = new Vector2Int(0,0);
            // TODO: Determine what's faster: math or checking with exact values?
            // Positive X (3,6,9)
            if(dirNum % 3 == 0){
                result.x = 1;
            }
            // Positive Y (7,8,9)
            if((dirNum-1) / 3 == 2){
                result.y = 1;
            }
            // Negative X (1,4,7)
            if(dirNum % 3 == 1){
                result.x = -1;
            }
            // Negative Y (1,2,3)
            if((dirNum-1) / 3 == 0){
                result.y = -1;
            }

            return result;
        }

        /*
         * InitGameState --
         *
         * Initialize our game state.
         */

        public SpGame(int num_players) {
            timestep = 1m / 60m;
            Framenumber = 0;
            objectIDMap = new Dictionary<int, GameObject>();
            currentLevel = 1;
            SpGame.endLevel = -1; //No winner

            // Create world
            _world = new PhysWorld();
            // Make it so that players can't collide with each other
            _world.collisionMatrix.SetLayerCollisions(Constants.coll_layers.player, Constants.coll_layers.player, false);
            // Make it so that players can't collide with noPlayer layer
            _world.collisionMatrix.SetLayerCollisions(Constants.coll_layers.player, Constants.coll_layers.noPlayer, false);

            // Create map
            GameObject mapPrefab = GameObject.Instantiate(Resources.Load<GameObject>($"_PREFAB/Maps/Map{currentLevel}"));
            mapPrefab.GetComponent<Map>().Initialize(_world);
            Map startMap = mapPrefab.GetComponent<Map>();
            fp3 startPos = startMap.StartPosition.physObj.Transform.Position;

            // Create and add new character objects
            _characters = new Character[num_players];
            for (int i = 0; i < _characters.Length; i++) {
                Tuple<GameObject, PhysObject> charTuple = _world.CreateAABBoxObject(
                    startPos, new fp3(1, 1, 1), true, true, Constants.GRAVITY * 2, Constants.coll_layers.player
                );
                charTuple.Item2.DynamicFriction = 0;
                charTuple.Item2.StaticFriction = 0;
                Character newChar = new Character(charTuple.Item2, _world, i+1);
                _characters[i] = newChar;
                int id = charTuple.Item1.GetInstanceID();
                _characters[i].instanceId = id;

                // Add object and id to map
                objectIDMap.Add(id, charTuple.Item1);

                // Assign color
                charTuple.Item1.GetComponent<Renderer>().material.color = i+1 == 1 ? Color.red : i+1 == 2 ? Color.blue : i+1 == 3 ? Color.yellow : Color.green;
            }


            // Spawn boxes
            _boxes = new Box[num_players];
            for (int i = 0; i < _boxes.Length; i++) {
                createBox(i);
            }
            

            // TODO: When creating objects, add to tuple with PhysObject/ObjectId/GameObject tuple
        }

        private Map SetUpMap(){
            // Maybe I should just create a new game? Nope... not a good idea
            int num_players = _characters.Length;

            CleanUp();
            GameObject mapPrefab = GameObject.Instantiate(Resources.Load<GameObject>($"_PREFAB/Maps/Map{currentLevel}"));
            mapPrefab.GetComponent<Map>().Initialize(_world);
            Map resultMap = mapPrefab.GetComponent<Map>();
            fp3 startPos = resultMap.StartPosition.physObj.Transform.Position;
            for(int i = 0; i < _characters.Length; i++){
                Character c = _characters[i];
                c.physObj.Transform.Position = startPos;
            }

            // Create and add new character objects
            _characters = new Character[num_players];
            for (int i = 0; i < _characters.Length; i++) {
                Tuple<GameObject, PhysObject> charTuple = _world.CreateAABBoxObject(
                    startPos, new fp3(1, 1, 1), true, true, Constants.GRAVITY * 2, Constants.coll_layers.player
                );
                charTuple.Item2.DynamicFriction = 0;
                charTuple.Item2.StaticFriction = 0;
                Character newChar = new Character(charTuple.Item2, _world, i+1);
                _characters[i] = newChar;
                int id = charTuple.Item1.GetInstanceID();
                _characters[i].instanceId = id;

                // Add object and id to map
                objectIDMap.Add(id, charTuple.Item1);

                // Assign color
                charTuple.Item1.GetComponent<Renderer>().material.color = i+1 == 1 ? Color.red : i+1 == 2 ? Color.blue : i+1 == 3 ? Color.yellow : Color.green;
            }


            // Spawn boxes
            _boxes = new Box[num_players];
            for (int i = 0; i < _boxes.Length; i++) {
                createBox(i);
            }


            SpGame.endLevel = -1;
            return resultMap;
        }

        // Player starts from 0
        private void createBox(int player){
            _boxes[player] = new Box();
            // Get new position and assign it
            Vector3Int pos = new Vector3Int(player, 0, 0);
            _boxes[player].position = pos;

            // Create game object and assign instance id
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = pos;
            int id =cube.GetInstanceID();
            _boxes[player].instanceId = id;

            // Add object and id to map
            objectIDMap.Add(id, cube);

            // Assign color
            cube.GetComponent<Renderer>().material.color = player+1 == 1 ? Color.red : player+1 == 2 ? Color.blue : player+1 == 3 ? Color.yellow : Color.green;
        }

        public void ParseBoxInputs(long inputs, int i, out int dir, out int dirThisFrame) {
            dir = 0; //TODO: Update
            int v = 0;
            int h = 0;

            // Vertical
            if(CheckInput(inputs, InputRegistry.INPUT_UP_THIS_FRAME))
                v = 1;
            else if(CheckInput(inputs, InputRegistry.INPUT_DOWN_THIS_FRAME))
                v = -1;

            // Horizontal
            if(CheckInput(inputs, InputRegistry.INPUT_RIGHT_THIS_FRAME))
                h = 1;
            else if(CheckInput(inputs, InputRegistry.INPUT_LEFT_THIS_FRAME))
                h = -1;

            dirThisFrame = GetDirNumber(v, h);
        }

        public void MoveBox(int index, int dirThisFrame) {
            var box = _boxes[index];

            if (dirThisFrame > 0) {
                Vector2Int dirVector = GetVectorFromDirNumber(dirThisFrame);
                int dx = dirVector.x;
                int dy = dirVector.y;
                int dz = 0;
                GGPORunner.LogGame("Moving box up.");
                box.position.x = box.position.x + dx;
                box.position.y = box.position.y + dy;
                box.position.z = box.position.z + dz;
                GGPORunner.LogGame($"New box position: {box.position.x},{box.position.y},{box.position.z}");
            }

            // Move physical box
            GameObject boxObj = GetObjectFromID(box.instanceId);
            if(!(boxObj is null))
                boxObj.transform.position = box.position;
        }

        public void LogInfo(string filename) {
            string fp = "";
            fp += "GameState object.\n";
            fp += string.Format("  bounds: {0},{1} x {2},{3}.\n", _bounds.xMin, _bounds.yMin, _bounds.xMax, _bounds.yMax);
            fp += string.Format("  num_boxes: {0}.\n", _boxes.Length);
            File.WriteAllText(filename, fp);
        }

        public void Update(long[] inputs, int disconnect_flags) {
            Framenumber++;
            // Box logic
            for (int i = 0; i < _boxes.Length; i++) {
                int dir, dirThisFrame = 0;

                if ((disconnect_flags & (1 << i)) != 0) {
                    //GetShipAI(i, out heading, out thrust, out fire);
                }
                else {
                    ParseBoxInputs(inputs[i], i, out dir, out dirThisFrame);
                }
                MoveBox(i, dirThisFrame);
            }
            // World update
            _world.Step(timestep);

            _world.UpdateGameObjects();

            // Game logic
            for (int i = 0; i < _characters.Length; i++) {
                _characters[i].Step(timestep, inputs[i]);
            }

            // End level
            if(SpGame.endLevel != -1){
                // TODO: increment score

                // TODO: Don't hardcode max number of levels
                currentLevel = ++currentLevel > 2 ? 1 : currentLevel;
                SetUpMap();
            }
        }

        public long ReadInputs(int id, long lastInputs) {
            return InputHandler.ih.ReadInputs(id, lastInputs);
        }

        public void FreeBytes(NativeArray<byte> data) {
            if (data.IsCreated) {
                data.Dispose();
            }
        }

        public override int GetHashCode() {
            int hashCode = -1214587014;
            hashCode = hashCode * -1521134295 + Framenumber.GetHashCode();
            foreach (var box in _boxes) {
                hashCode = hashCode * -1521134295 + box.GetHashCode();
            }
            return hashCode;
        }
    }
}