using System;
using ChessChallenge.API;
using System.Numerics;

public class MyBot : IChessBot
{

    struct MoveValue
    {
        public MoveValue(Move m, int v)
        {
            move = m;
            value = v;
        }

        public readonly Move move;
        public readonly int value;
    }
    
    /// Piece values: pawn, knight, bishop, rook, queen, king
    int[] pieceValues = { 100, 300, 350, 500, 900, 0};

    static Board board;
    Timer timer;
    int NEGATIVE_INFINITY = -99999999;
    int POSITIVE_INFINITY = 99999999;
    int CHECKMATE_EVAL = -9999999;

    static int DistanceFromEdgeOfBoard(int x)
    {
        return Math.Min(7 - x, x);
    }

    static int DistanceFromEdgeOfBoard(Square square) 
    {
        return DistanceFromEdgeOfBoard(square.File) + DistanceFromEdgeOfBoard(square.Rank);
    }

    // functions that attempt to simulate a piece square table
    //           square, return
    private static Func<Square,  int>[] pieceSquareEstimaters = {
        (square =>  // PAWN
        
            /*
            0, 0, 0, 0, 0, 0, 0, 0,
            50, 50, 50, 50, 50, 50, 50, 50,
            10, 10, 20, 30, 30, 20, 10, 10,
            5, 5, 10, 25, 25, 10, 5, 5,
            0, 0, 0, 20, 20, 0, 0, 0,
            5, -5, -10, 0, 0, -10, -5, 5,
            5, 10, 10, -20, -20, 10, 10, 5,
            0, 0, 0, 0, 0, 0, 0, 0
            */
            // if we are not on rank 1, subtract 2 from the rank and scale by 10
            // else if we are on d2 or e2, return -20
            // else return 10
            //(square.Rank - 2) * 10
            square.Rank > 1 ? (square.Rank - 2) * 10 : DistanceFromEdgeOfBoard(square.File) == 3 ? -20 : 10
        ),
        (square =>  // KNIGHT
        
            /*
          -50, -40, -30, -30, -30, -30, -40, -50,
          -40, -20, 0, 5, 5, 0, -20, -40,
          -30, 5, 10, 15, 15, 10, 5, -30,
          -30, 0, 15, 20, 20, 15, 0, -30,
          -30, 5, 15, 20, 20, 15, 5, -30,
          -30, 0, 10, 15, 15, 10, 0, -30,
          -40, -20, 0, 0, 0, 0, -20, -40,
          -50, -40, -30, -30, -30, -30, -40, -50
          */
            
            DistanceFromEdgeOfBoard(square) * 10 - 40
        ),
        (square =>  // BISHOP
        
            // same as the knight
            pieceSquareEstimaters[1](square)
            
        ),
        (square =>  // ROOK
        
            /*
          0, 0, 0, 0, 0, 0, 0, 0,
          5, 10, 10, 10, 10, 10, 10, 5,
          -5, 0, 0, 0, 0, 0, 0, -5,
          -5, 0, 0, 0, 0, 0, 0, -5,
          -5, 0, 0, 0, 0, 0, 0, -5,
          -5, 0, 0, 0, 0, 0, 0, -5,
          -5, 0, 0, 0, 0, 0, 0, -5,
           0, 0, 0, 5, 5, 0, 0, 0
             */
            square.Rank == 6 ? 10 : square.File % 7 != 0 ? 0 : square.Rank == 0 ? 0 : -5
        ),
        (square =>  // QUEEN
        

            /*-20, -10, -10, -5, -5, -10, -10, -20,
            -10, 0, 0, 0, 0, 5, 0, -10,
            -10, 0, 5, 5, 5, 5, 5, -10,
            -5, 0, 5, 5, 5, 5, 0, 0,
            -5, 0, 5, 5, 5, 5, 0, -5,
            -10, 0, 5, 5, 5, 5, 0, -10,
            -10, 0, 0, 0, 0, 0, 0, -10,
            -20, -10, -10, -5,-5, -10, -10, -20*/
            
            pieceSquareEstimaters[1](square)
        ),
        (square => // KING
            
                //int mgStrength = 6-DistanceFromEdgeOfBoard(square);
                //int egStrength = DistanceFromEdgeOfBoard(square);
                //int numPieces = BitOperations.PopCount(board.AllPiecesBitboard);
                //return (mgStrength * numPieces + egStrength * (32 - numPieces)) / 32 * 10;
                
                (6-DistanceFromEdgeOfBoard(square)) * BitOperations.PopCount(board.AllPiecesBitboard) + 
                    DistanceFromEdgeOfBoard(square) * (32-BitOperations.PopCount(board.AllPiecesBitboard)) / 3
            
        ),
        
    };
    
    
    public Move Think(Board pboard, Timer ptimer)
    {
        board = pboard;
        timer = ptimer;
        Move bestMove = Move.NullMove;
        Move bestMoveThisIteration = Move.NullMove;
        int depth = 0;
        while (!ShouldFinishSearch())
        {
            bestMove = bestMoveThisIteration;
            depth++;

            bestMoveThisIteration = NegaMax(depth, NEGATIVE_INFINITY, POSITIVE_INFINITY).move;
        }
        
        return bestMove;
    }

    MoveValue NegaMax(int depth, int alpha, int beta)
    {
        Move bestMove = Move.NullMove;
        if (board.IsInCheckmate()) return new MoveValue(bestMove, CHECKMATE_EVAL - depth);
        if (board.IsDraw()) return new MoveValue(bestMove, -50);

        if (depth <= 0)
        {
            int currentEval = EvaluateBoard();
            if (currentEval >= beta) return new MoveValue(bestMove, beta);
            if (currentEval > alpha) alpha = currentEval;
        }

        
        
        var moves = board.GetLegalMoves(depth<=0);
        sortMoves(ref moves);
        
        foreach (Move move in moves)
        {
            board.MakeMove(move);
            int newEval = -NegaMax(depth - 1, -beta, -alpha).value;
            board.UndoMove(move);

            if (ShouldFinishSearch()) break;

            if (newEval >= beta) return new MoveValue(move, beta);

            if (newEval > alpha)
            {
                bestMove = move;
                alpha = newEval;
            }
        }

        return new MoveValue(bestMove, alpha);
    }

    int EvaluateBoard()
    {
        // sum piece
        int res = 0;
        var pieceLists = board.GetAllPieceLists();
        for (int pieceType = 0; pieceType<6; pieceType++)
        {
            // PIECE SQUARE VALUE SUMS
            res += (pieceLists[pieceType].Count - pieceLists[pieceType+6].Count) * pieceValues[pieceType];

            // PIECE SQUARE ESTIMATES
            ulong friendlyBB = board.GetPieceBitboard((PieceType)pieceType+1, true);
            ulong enemyBB = board.GetPieceBitboard((PieceType)pieceType+1, false);
            while (friendlyBB > 0)
                res += pieceSquareEstimaters[pieceType](
                       new Square(BitboardHelper.ClearAndGetIndexOfLSB(ref friendlyBB)));
            while (enemyBB > 0)
                res -= pieceSquareEstimaters[pieceType](// xor with 56 flips the index of the square to treat it as if it was for the other team
                    new Square(BitboardHelper.ClearAndGetIndexOfLSB(ref enemyBB)^56));

        }
        
        
        
        if (!board.IsWhiteToMove) res = -res;
        return res;
    }

    int GetPieceValue(Square sq)
    {
        return pieceValues[(int)board.GetPiece(sq).PieceType - 1];
    }

    void sortMoves(ref Move[] moves)
    {

        var moveScores = new int[moves.Length];
        for (int i = 0; i < moves.Length; i++)
        {
            Move move = moves[i];
            if (move.IsCapture)
            {
                moveScores[i] += GetPieceValue(move.TargetSquare) - GetPieceValue(move.StartSquare) / 10;

            }
            
            // negate so that the moves get sorted best to worst
            moveScores[i] *= -1;
        }
        
        Array.Sort(moveScores, moves);
    }
    

    bool ShouldFinishSearch()
    {
        return timer.MillisecondsElapsedThisTurn > 100;
    }
    
    
}
