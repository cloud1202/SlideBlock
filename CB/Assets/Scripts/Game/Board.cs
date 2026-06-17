using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using static UnityEngine.InputSystem.InputAction;


public class Board : RoundObject
{
    public int MatchCount = 3;
    public const int BOARD_SIZE = 7;
    private int MATCH_COUNT => MatchCount;
    private const int INIT_BRICK_COUNT = 4;
    private const float TOUCH_GAP = 0.5f;
    private const float TOUCH_LENGTH = 150f;
    private readonly int BRICK_TYPES = EnumConverter.Enum32ToInt(BrickType.MAX);
    private readonly float[] POS_ARR = new float[] { -2.1f, -1.4f, -0.7f, 0f, 0.7f, 1.4f, 2.1f };
    private bool _isDrag = false;
    private bool _isSlide = false;
    private Vector2 _beginPos = Vector2.zero;
    private BoardArea[,] _boardAreas = new BoardArea[BOARD_SIZE, BOARD_SIZE];
    private Queue<Brick> _bricks = new Queue<Brick>();

    private (int r, int c)[] offset = new (int r, int c)[4]
    {
        (-1, 0),    // Up 
        (0, -1),    // Right
        (1, 0),     // Down
        (0, 1)      // Left
    };

    enum BoardDirection
    {
        None,
        Up,
        Right,
        Down,
        Left
    }
    private BoardDirection _boardDirection = BoardDirection.None;

    private void Awake()
    {
        InputManager.Instance.SubscribeToInputHandler(InputType.Player_Touch, OnTouchPoint, cancel: OnEndTouchPoint);
        InputManager.Instance.SubscribeToInputHandler(InputType.Player_Point, perform: OnDragPoint);

        _boardDirection = BoardDirection.None;
        _bricks.Clear();
    }

    private void OnDestroy()
    {
        InputManager.Instance.UnsubscribeToInputHandler(InputType.Player_Touch, OnTouchPoint, cancel: OnEndTouchPoint);
        InputManager.Instance.UnsubscribeToInputHandler(InputType.Player_Point, perform: OnDragPoint);
    }

    public override void Init()
    {
        for (int i = 0; i < BOARD_SIZE; i++)
            for (int j = 0; j < BOARD_SIZE; j++)
                _boardAreas[i, j].Init(i, j, POS_ARR[i], POS_ARR[j]);

        InitBrick().Forget();
    }

    async private UniTask InitBrick()
    {
        int initCnt = (BOARD_SIZE * BOARD_SIZE) - _bricks.Count;
        for (int i = 0; i < initCnt; i++)
        {
            var brick = await PrefabManager.Instance.InstantiateObject<Brick>(PrefabData.Brick, this.transform);
            brick.gameObject.SetActive(false);
            _bricks.Enqueue(brick);
        }

        var areas = GetEmptyAreas(INIT_BRICK_COUNT);

        for(int i =0; i < areas.Count; ++i)
        {
            SpawnBrick(areas[i]);
        }
    }

    private bool TrySpawnBrick()
    {
        var areas = GetEmptyAreas();
        int cnt = areas.Count;
        for (int i = 0; i < cnt; ++i)
        {
            SpawnBrick(areas[i]);
        }

        return cnt > 0;
    }
     
    private void SpawnBrick(BoardArea area)
    {
        var brick = _bricks.Dequeue();
        BrickType type = EnumConverter.IntToEnum32<BrickType>(Utility.RandomInt(BRICK_TYPES));
        brick.Init(type, area.GetPos());
        _boardAreas[area.row, area.col].SetBrick(brick);
    }

    private void OnTouchPoint(CallbackContext context)
    {
        if (_isSlide)
            return;
        _isDrag = true;
    }

    private void OnDragPoint(CallbackContext context)
    {
        if (_isSlide)
            return;
        if (_isDrag == false)
        {
            _beginPos = context.ReadValue<Vector2>();
            return;
        }
        var pos = context.ReadValue<Vector2>();

        var dir = (pos - _beginPos);
        if (dir.magnitude <= TOUCH_LENGTH)
            return;

        _isDrag = false;
        if (dir.normalized.x < -TOUCH_GAP)
            _boardDirection = BoardDirection.Left;
        else if (dir.normalized.x > TOUCH_GAP)
            _boardDirection = BoardDirection.Right;
        else if (dir.normalized.y < -TOUCH_GAP)
            _boardDirection = BoardDirection.Down;
        else if (dir.normalized.y > TOUCH_GAP)
            _boardDirection = BoardDirection.Up;

        ChangeBoardDirection().Forget();
    }

    private void OnEndTouchPoint(CallbackContext context)
    {
        _beginPos = Vector2.zero;
        _isDrag = false;
    }

    async private UniTask ChangeBoardDirection()
    {
        _isSlide = true;
        bool anyDestroyed = false;

        do
        {
            SlideAll();
            await UniTask.WaitForSeconds(0.2f);
            anyDestroyed = await DestroyMatches();
        }
        while (anyDestroyed);

        bool isCompleteSpawn = TrySpawnBrick();

        if (isCompleteSpawn == false)
        {
            _roundManager.EndRound();
        }

        _isSlide = false;
    }

    private void SlideAll()
    {
        for (int lineIndex = 0; lineIndex < BOARD_SIZE; ++lineIndex)
        {
            var (startRow, startCol, dRow, dCol, count) = GetLineConfig(lineIndex);
            SlideOneLine(startRow, startCol, dRow, dCol, count);
        }
    }

    private void SlideOneLine(int startRow, int startCol, int dRow, int dCol, int count)
    {
        int writeR = startRow, writeC = startCol;

        for (int i = 0; i < count; ++i)
        {
            int readR = startRow + dRow * i;
            int readC = startCol + dCol * i;

            if (_boardAreas[readR, readC].isEmpty) continue;

            if (readR != writeR || readC != writeC)
            {
                Brick brick = _boardAreas[readR, readC].brick;
                brick.Move(_boardAreas[writeR, writeC].GetPos());
                _boardAreas[writeR, writeC].SetBrick(brick);
                _boardAreas[readR, readC].SetBrick();
            }

            writeR += dRow;
            writeC += dCol;
        }
    }

    private (int startRow, int startCol, int dRow, int dCol, int lineCount)
        GetLineConfig(int lineIndex) => _boardDirection switch
        {
            BoardDirection.Up       => (BOARD_SIZE - 1, lineIndex, offset[0].r, offset[0].c, BOARD_SIZE),
            BoardDirection.Right    => (lineIndex, BOARD_SIZE - 1, offset[1].r, offset[1].c, BOARD_SIZE),
            BoardDirection.Down     => (0, lineIndex, offset[2].r, offset[2].c, BOARD_SIZE),
            BoardDirection.Left     => (lineIndex, 0, offset[3].r, offset[3].c, BOARD_SIZE),
            _ => throw new ArgumentException()
        };

    async private UniTask<bool> DestroyMatches()
    {
        List<List<Brick>> toDestroyBricks = new List<List<Brick>>();
        for(int i = 0; i < BOARD_SIZE; ++i)
        {
            for (int j = 0; j < BOARD_SIZE; ++j)
            {
                if (_boardAreas[i, j].isEmpty)
                    continue;
                var bricks = new List<Brick>();
                DFSMatchBrick(i, j, ref bricks);
                if (bricks.Count < MATCH_COUNT)
                    continue;

                toDestroyBricks.Add(bricks);
            }
        }

        for (int i = 0; i < toDestroyBricks.Count; ++i)
        {
            int score = toDestroyBricks[i].Count * 10;
            Bounds bounds = new Bounds(toDestroyBricks[i][0].transform.position, Vector3.zero);
            toDestroyBricks[i].ForEach(b =>
            {
                b.Destroy();
                _bricks.Enqueue(b);
                bounds.Encapsulate(b.transform.position);
            });

            _roundManager.DestroyMatchBricks(score, bounds.center);
        }


        if (toDestroyBricks.Count > 0)
        {
            await UniTask.WaitForSeconds(0.4f);
            return true;
        }
        else
        {
            _roundManager.DestroyMatchBricks(0, Vector2.zero);
            return false;
        }
    }

    private void DFSMatchBrick(int row, int col, ref List<Brick> bricks)
    {
        Brick brick = _boardAreas[row, col].brick;
        _boardAreas[row, col].SetBrick();
        bricks.Add(brick);
        for (int i = 0; i < 4; ++i)
        {
            int checkR = row + offset[i].r;
            int checkC = col + offset[i].c;

            if (checkR < 0 || checkC < 0 || checkR >= BOARD_SIZE || checkC >= BOARD_SIZE)
                continue;

            if (_boardAreas[checkR, checkC].isEmpty)
                continue;

            if (_boardAreas[checkR, checkC].MatchBrickType(brick) == false)
                continue;

            DFSMatchBrick(checkR, checkC, ref bricks);

        }
        if (bricks.Count < MATCH_COUNT)
        {
            _boardAreas[row, col].SetBrick(brick);
            bricks.Remove(brick);
        }
    }

    private List<BoardArea> GetEmptyAreas(int cnt = 2)
    {
        List<BoardArea> _targetAreas = new List<BoardArea>();
        List<BoardArea> _emptyAreas = new List<BoardArea>();

        switch (_boardDirection)
        {
            case BoardDirection.Up:
                for (int i = 0; i < BOARD_SIZE; ++i)
                    if (_boardAreas[0, i].isEmpty)
                        _targetAreas.Add(_boardAreas[0, i]);
                break;
            case BoardDirection.Right:
                for (int i = 0; i < BOARD_SIZE; ++i)
                    if (_boardAreas[i, 0].isEmpty)
                        _targetAreas.Add(_boardAreas[i, 0]);
                break;
            case BoardDirection.Down:
                for (int i = 0; i < BOARD_SIZE; ++i)
                    if (_boardAreas[BOARD_SIZE - 1, i].isEmpty)
                        _targetAreas.Add(_boardAreas[BOARD_SIZE - 1, i]);
                break;
            case BoardDirection.Left:
                for (int i = 0; i < BOARD_SIZE; ++i)
                    if (_boardAreas[i, BOARD_SIZE - 1].isEmpty)
                        _targetAreas.Add(_boardAreas[i, BOARD_SIZE - 1]);
                break;
            case BoardDirection.None:
                for (int i = 0; i < BOARD_SIZE; ++i)
                    for (int j = 0; j < BOARD_SIZE; ++j)
                        _targetAreas.Add(_boardAreas[i, j]);
                break;
        }

        for(int i = 0; i < cnt; ++i)
        {
            if (_targetAreas.Count == 0)
                break;

            int randIndex = Utility.RandomInt(_targetAreas.Count);
            _emptyAreas.Add(_targetAreas[randIndex]);
            _targetAreas.RemoveAt(randIndex);
        }

        return _emptyAreas;
    }
}
