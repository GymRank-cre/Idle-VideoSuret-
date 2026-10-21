import pytest

from clipengine.errors import UserError
from clipengine.media import MediaInfo, check_info, check_remote, check_url, video_key


def test_video_key_identifies_the_same_video_across_link_forms():
    k = "yt:dQw4w9WgXcQ"
    for u in ("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "https://youtu.be/dQw4w9WgXcQ?t=42",
              "https://m.youtube.com/watch?v=dQw4w9WgXcQ&feature=share", "https://www.youtube.com/shorts/dQw4w9WgXcQ",
              "https://music.youtube.com/watch?v=dQw4w9WgXcQ&list=x", "https://www.youtube.com/embed/dQw4w9WgXcQ"):
        assert video_key(u) == k, u
    assert video_key("https://www.twitch.tv/videos/123456789?t=1h") == "tw:123456789"
    assert video_key("https://vimeo.com/1/a?x=1") != video_key("https://vimeo.com/1/a?x=2")
    assert video_key("https://vimeo.com/1/a?x=1") == video_key("https://vimeo.com/1/a?x=1")
    assert video_key("https://www.youtube.com/watch?v=aaaaaaaaaaa") != video_key("https://www.youtube.com/watch?v=bbbbbbbbbbb")


@pytest.mark.parametrize("url", ["http://127.0.0.1/x", "http://localhost:8080/", "http://169.254.169.254/latest/meta-data",
                                 "http://10.0.0.5/a", "http://192.168.1.10/a", "http://[::1]/x", "http://0.0.0.0/",
                                 "http://172.16.3.4/x"])
def test_check_url_refuses_internal_addresses(url):
    with pytest.raises(UserError, match="interne"):
        check_url(url)


@pytest.mark.parametrize("url", ["file:///etc/passwd", "ftp://example.com/x", "javascript:alert(1)", "", "pas un lien",
                                 "http:///x"])
def test_check_url_refuses_other_schemes(url):
    with pytest.raises(UserError):
        check_url(url)


def test_check_url_accepts_a_public_address():
    assert check_url(" https://8.8.8.8/watch?v=1 ") == "https://8.8.8.8/watch?v=1"


def test_check_remote_rules():
    check_remote({"duration": 3600})
    with pytest.raises(UserError, match="Direct"):
        check_remote({"is_live": True, "duration": None})
    with pytest.raises(UserError, match="Direct"):
        check_remote({"live_status": "is_upcoming"})
    with pytest.raises(UserError, match="playlist"):
        check_remote({"_type": "playlist", "entries": [1]})
    with pytest.raises(UserError, match="trop longue"):
        check_remote({"duration": 5 * 3600})
    with pytest.raises(UserError, match="trop courte"):
        check_remote({"duration": 5})


def test_check_info_rules():
    ok = MediaInfo(1920, 1080, 30, 600, True)
    check_info(ok)
    with pytest.raises(UserError, match="trop courte"):
        check_info(MediaInfo(1920, 1080, 30, 10, True))
    with pytest.raises(UserError, match="trop longue"):
        check_info(MediaInfo(1920, 1080, 30, 5 * 3600, True))
    with pytest.raises(UserError, match="piste audio"):
        check_info(MediaInfo(1920, 1080, 30, 600, False))


def test_download_errors_are_explained_not_misattributed():
    from clipengine.media import explain_download_error as ex
    bot = ("ERROR: [youtube] lxGfvKYYs1U: Sign in to confirm you’re not a bot. Use --cookies-from-browser or --cookies "
           "for the authentication.")
    assert "anti-robot" in ex(bot) and "CLIPENGINE_COOKIES" in ex(bot) and "limite d'âge" not in ex(bot)
    assert "limite d'âge" in ex("ERROR: Sign in to confirm your age. This video may be inappropriate for some users.")
    assert "privée" in ex("ERROR: Private video. Sign in if you've been granted access to this video")
    assert "pays" in ex("ERROR: The uploader has not made this video available in your country")
    assert "n'existe plus" in ex("ERROR: Video unavailable. This video has been removed by the uploader")
    out = ex("ERROR: something about a message and a page")           # « age » dans « message » ou « page » : pas de faux positif
    assert out.startswith("Téléchargement impossible") and "limite d'âge" not in out
