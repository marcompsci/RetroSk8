using RetroSk8.Core;
using RetroSk8.Level;

namespace RetroSk8.Game
{
    /// <summary>Starts a Trick Book lesson (Phase 17): Free Skate at the lesson's park with the lesson coach on.</summary>
    public static class LessonService
    {
        public static void Start(string lessonId)
        {
            var lesson = TrickLessons.Find(lessonId);
            if (lesson == null) return;
            GameSession.LessonId = lesson.Id;
            GameSession.Mode = RunMode.FreeSkate;
            GameSession.Challenge = null;
            GameSession.EditPark = false;
            SceneRouter.LoadPark(lesson.ParkId, ParkCatalog.SceneFor(lesson.ParkId));
        }
    }
}
