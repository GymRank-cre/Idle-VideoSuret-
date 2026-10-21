"""clipengine : transforme une vidéo longue en clips verticaux sous-titrés."""

from .config import Config
from .models import Candidate, Sentence, Word

__all__ = ["Config", "Candidate", "Sentence", "Word"]
__version__ = "0.1.0"
