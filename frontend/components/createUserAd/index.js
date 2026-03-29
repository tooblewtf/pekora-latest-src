// From https://www.youtube.com/watch?v=1Lb0sNikXec
import { useRef, useState } from "react";
import { createUseStyles } from "react-jss";
import { uploadAdvertisement } from "../../services/ads";
import useButtonStyles from "../../styles/buttonStyles";
import ActionButton from "../actionButton";

const useStyles = createUseStyles({
  heading: {
    fontWeight: 600,
  },
  buttonList: {
    maxWidth: '400px',
  },
  downloadButton: {
    fontSize: '1rem',
    marginTop: '-1.5rem',
  },
  inputLabel: {
    fontWeight: 600,
  },
  createUserAdContainer: {
    background: 'var(--white-color)',
  },
});

const templates = {
  banner: '/img/058f09be8a19b8607d6fe50baa6659a1-AdBannerTemplate.png',
  skyScraper: '/img/b52c03d7260a8d693e6fc4e4fa16febd-AdSkyscraperTemplate.png',
  rectangle: '/img/fe2d40a3cec3899c9a91838ee2c16077-AdRectangleTemplate.png',
}

const CreateUserAd = props => {
  const s = useStyles();
  const btnStyles = useButtonStyles();
  const nameRef = useRef(null);
  const fileRef = useRef(null);
  const [locked, setLocked] = useState(false);
  const [feedback, setFeedback] = useState(null);

  const clicker = (type) => {
    return (e) => {
      e.preventDefault();
      window.location.href = type
    }
  }

  if (props.targetType !== 'asset' && props.targetType !== 'group') return <p>TargetType is not supported</p>
  return <div className={'container ' + s.createUserAdContainer}>
    <div className='row'>
      <div className='col-12'>
        <h1 className={s.heading + ' ps-4'}>Create a User Ad</h1>
      </div>
      <div className='col-12 mt-4'>
        <h2>Instructions</h2>
        <p>On Marine, users can bid an amount of robux to buy advertising for their places, groups, clothing, and models.</p>
        <p>Download, Edit, and Upload one of the following templates:</p>
        <ul className={s.buttonList}>
          <li className='mb-2'>
            728 x 90 Banner <ActionButton onClick={clicker(templates.banner)} className={btnStyles.continueButton + ' ' + btnStyles.normal + ' ' + s.downloadButton} label='Download'/>
          </li>
          <li className='mb-2'>
            160 x 600 Skyscraper <ActionButton onClick={clicker(templates.skyScraper)} className={btnStyles.continueButton + ' ' + btnStyles.normal + ' ' + s.downloadButton} label='Download'/>
          </li>
          <li>
            300 x 250 Rectangle <ActionButton onClick={clicker(templates.rectangle)} className={btnStyles.continueButton + ' ' + btnStyles.normal + ' ' + s.downloadButton} label='Download'/>
          </li>
        </ul>
      </div>
    </div>
    <div className='row mt-4'>
      <div className='col-12 col-lg-4'>
        <div className='row mb-2'>
          <div className='col-6'>
            <p className={s.inputLabel}>Upload an Ad</p>
          </div>
          <div className='col-6'>
            <input disabled={locked} ref={fileRef} type='file'/>
          </div>
        </div>
        <div className='row'>
          <div className='col-6'>
            <p className={s.inputLabel}>Name Your Ad</p>
          </div>
          <div className='col-6'>
            <input disabled={locked} ref={nameRef} type='text' className='pt-1 pb-1 pe-0 ps-0'/>
          </div>
        </div>
      </div>
    </div>
    <div className='row mt-2'>
      <div className='col-12'>
        {feedback && <p className='text-danger'>{feedback}</p>}
      </div>
      <div className='col-12'>
        <ActionButton disabled={locked} className={btnStyles.buyButton + ' float-left ' + btnStyles.normal} label='Upload' onClick={() => {
          setLocked(true);
          setFeedback(null);
          uploadAdvertisement({
            file: fileRef.current.files[0],
            name: nameRef.current.value,
            targetId: props.targetId,
            type: props.targetType,
          }).then(() => {
            window.location.href = '/develop?View=8';
          }).catch(e => {
            setLocked(false);
            setFeedback('Could not upload advertisement. ' + (!e.response ? 'If you have an ad blocker enabled, you may have to temporarily disable it.' : 'Error Message: ' + (e.response.data?.errors[0]?.message || e.message)));
          })
        }} />
      </div>
    </div>
    <p className='text-muted mt-2'>The ad needs to be approved by a Moderator before it can be launched from your Ad page.</p>

  </div>
}

export default CreateUserAd;